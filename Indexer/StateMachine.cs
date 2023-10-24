using System.Collections.Immutable;
using Circles.Index.Data.Cache;
using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
using Circles.Index.Pathfinder;
using Circles.Index.Utils;
using Microsoft.Data.Sqlite;
using Nethermind.Api;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Circles.Index.Indexer;

public class StateMachine
{
    public static readonly string _zeroAddress = Address.Zero.ToString(true, false);

    public class Context
    {
        public Context(
            string dbLocation
            , INethermindApi nethermindApi
            , ILogger logger
            , long lastIndexHeight
            , long currentChainHeight
            , long lastReorgAt
            , MemoryCache memoryCache
            , Sink sink
            , CancellationTokenSource cancellationTokenSource
            , Settings settings)
        {
            DbLocation = dbLocation;
            NethermindApi = nethermindApi;
            Logger = logger;
            LastIndexHeight = lastIndexHeight;
            CurrentChainHeight = currentChainHeight;
            LastReorgAt = lastReorgAt;
            MemoryCache = memoryCache;
            Sink = sink;
            CancellationTokenSource = cancellationTokenSource;
            Settings = settings;
        }

        public string DbLocation { get; }
        public INethermindApi NethermindApi { get; }
        public ILogger Logger { get; }

        public long LastIndexHeight { get; set; }
        public long CurrentChainHeight { get; set; }
        public long LastReorgAt { get; set; }

        public List<Exception> Errors { get; } = new();
        public MemoryCache MemoryCache { get; }
        public Sink Sink { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public Settings Settings { get; }
        public int PendingPathfinderUpdates;
    }

    private State _currentState = State.New;
    private readonly Context _context;

    public enum State
    {
        New,
        Initial,
        Syncing,
        Reorg,
        WaitForNewBlock,
        Error,
        End
    }

    public enum Event
    {
        SyncCompleted,
        NewBlock,
        ErrorOccurred,
        EnterState,
        LeaveState,
        ReorgCompleted
    }

    public StateMachine(Context context)
    {
        _context = context;
    }

    public async Task HandleEvent(Event e)
    {
        _context.Logger.Info($"Event {e} received in state {_currentState}");
        try
        {
            switch (_currentState)
            {
                case State.New:
                    // Empty state, only used to transition to the initial state
                    break;
                case State.Initial:
                    switch (e)
                    {
                        case Event.EnterState:
                        {
                            // Make sure all tables exist before we start
                            MigrateTables();

                            SetCurrentChainAndIndexHeights();

                            await using SqliteConnection reorgConnection = new($"Data Source={_context.DbLocation}");
                            await reorgConnection.OpenAsync();
                            await ReorgHandler.ReorgAt(reorgConnection, _context.MemoryCache, _context.Logger, Math.Min(_context.LastIndexHeight, _context.CurrentChainHeight));

                            SetCurrentChainAndIndexHeights();
                            WarmupCache();

                            await TransitionTo(_context.CurrentChainHeight == _context.LastIndexHeight
                                ? State.WaitForNewBlock
                                : State.Syncing);
                            break;
                        }
                    }

                    break;

                case State.Syncing:
                    switch (e)
                    {
                        case Event.EnterState:
                            // Make sure we know what exactly we are syncing
                            SetCurrentChainAndIndexHeights();

                            // Internally runs asynchronous and syncs the whole backlog of missed blocks.
                            // After that the method will trigger SyncComplete and stop executing.
                            // It will only be restarted once the state is entered again.
                            Sync();
                            break;
                        case Event.SyncCompleted:
                            _context.Errors.Clear(); // Clean up errors after a successful sync

                            // TODO: Should only be done once, not every time the event is triggered
                            MigrateIndexes(); // Make sure that the indexes are only created once the syncing is complete
                            UpdatePathfinder();

                            await TransitionTo(State.WaitForNewBlock);
                            break;
                    }

                    break;

                case State.WaitForNewBlock:
                    switch (e)
                    {
                        case Event.NewBlock:
                            long? reorgAt = TryFindReorg();
                            if (reorgAt != null)
                            {
                                _context.LastReorgAt = reorgAt.Value;
                                await TransitionTo(State.Reorg);
                            }
                            else
                            {
                                await TransitionTo(State.Syncing);
                            }

                            break;
                    }

                    break;

                case State.Reorg:
                    switch (e)
                    {
                        case Event.EnterState:
                            // Internally runs asynchronous, deletes all state after the reorg block and triggers a ReorgCompleted event when done.
                            // After that the method will stop executing and only be restarted once the state is entered again.
                            Reorg();
                            break;
                        case Event.ReorgCompleted:
                            await TransitionTo(State.Syncing);
                            break;
                    }

                    break;

                case State.Error:
                    switch (e)
                    {
                        case Event.EnterState:
                            await TransitionTo(_context.Errors.Count >= 3
                                ? State.End
                                : State.Initial);
                            break;
                        case Event.LeaveState:
                            Cleanup();
                            break;
                    }

                    break;

                case State.End:
                    Cleanup();
                    break;
            }
        }
        catch (Exception ex)
        {
            _context.Logger.Error($"Error while handling {e} event in state {_currentState}", ex);
            _context.Errors.Add(ex);

            await HandleEvent(Event.ErrorOccurred);
        }
    }

    #region Context changing methods

    private void SetCurrentChainAndIndexHeights()
    {
        if (_context.NethermindApi.BlockTree?.Head == null)
        {
            throw new Exception("BlockTree.Head is null");
        }

        using SqliteConnection mainConnection = new($"Data Source={_context.DbLocation}");
        mainConnection.Open();

        _context.LastIndexHeight = Query.LatestBlock(mainConnection) ?? 0;
        _context.Logger.Info($"Current index height: {_context.LastIndexHeight}");

        _context.CurrentChainHeight = _context.NethermindApi.BlockTree.Head.Number;
        _context.Logger.Info($"Current chain height: {_context.CurrentChainHeight}");
    }

    private void Cleanup()
    {
    }

    #endregion

    private async void Reorg()
    {
        _context.Logger.Info("Starting reorg process.");
        if (_context.LastReorgAt == 0)
        {
            throw new Exception("LastReorgAt is 0");
        }

        await using SqliteConnection connection = new($"Data Source={_context.DbLocation}");
        connection.Open();
        await ReorgHandler.ReorgAt(connection, _context.MemoryCache, _context.Logger, _context.LastReorgAt);

        await HandleEvent(Event.ReorgCompleted);
    }

    public async Task TransitionTo(State newState)
    {
        _context.Logger.Info($"Transitioning from {_currentState} to {newState}");
        await HandleEvent(Event.LeaveState);

        _currentState = newState;

        await HandleEvent(Event.EnterState);
    }

    private async void Sync()
    {
        _context.Logger.Info("Starting syncing process.");

        if (_context.NethermindApi.BlockTree == null)
        {
            throw new Exception("BlockTree is null");
        }

        if (_context.NethermindApi.ReceiptFinder == null)
        {
            throw new Exception("ReceiptFinder is null");
        }

        await BlockIndexer.IndexBlocks(
            _context.NethermindApi.BlockTree,
            _context.NethermindApi.ReceiptFinder,
            _context.MemoryCache,
            _context.Sink,
            _context.Logger,
            GetBlocksToSync(),
            _context.CancellationTokenSource.Token,
            _context.Settings);

        _context.Sink.Flush();

        await HandleEvent(Event.SyncCompleted);
    }

    private IEnumerable<long> GetBlocksToSync()
    {
        (ImmutableHashSet<long> KnownBlocks, long MaxKnownBlock, long MinKnownBlock) relevantBlocks
            = StaticResources.GetKnownRelevantBlocks(_context.Settings.ChainId);

        long nextIndexBlock = _context.LastIndexHeight == 0 ? 0 : _context.LastIndexHeight + 1;
        long from = (relevantBlocks.MinKnownBlock > -1 && _context.LastIndexHeight <= relevantBlocks.MinKnownBlock)
            ? relevantBlocks.MinKnownBlock
            : nextIndexBlock;

        _context.Logger.Info($"Enumerating blocks to sync from {from} to {_context.CurrentChainHeight}");

        for (long i = from; i <= _context.CurrentChainHeight; i++)
        {
            if (i <= relevantBlocks.MaxKnownBlock && !relevantBlocks.KnownBlocks.Contains(i))
            {
                continue;
            }

            yield return i;
        }
    }

    private void WarmupCache()
    {
        _context.Logger.Info("Warming up cache");
        using SqliteConnection connection = new($"Data Source={_context.DbLocation}");
        connection.Open();

        IEnumerable<CirclesSignupDto> signups = Query.CirclesSignups(connection, new CirclesSignupQuery { SortOrder = SortOrder.Ascending }, int.MaxValue);
        foreach (CirclesSignupDto signup in signups)
        {
            _context.MemoryCache.SignupCache.Add(signup.CirclesAddress, signup.TokenAddress);
        }

        IEnumerable<CirclesTrustDto> trusts = Query.CirclesTrusts(connection, new CirclesTrustQuery { SortOrder = SortOrder.Ascending }, int.MaxValue);
        foreach (CirclesTrustDto trust in trusts)
        {
            _context.MemoryCache.TrustGraph.AddOrUpdateEdge(trust.UserAddress, trust.CanSendToAddress, trust.Limit);
        }

        IEnumerable<CirclesTransferDto> transfers = Query.CirclesTransfers(connection, new CirclesTransferQuery { SortOrder = SortOrder.Ascending }, int.MaxValue);
        foreach (CirclesTransferDto transfer in transfers)
        {
            if (transfer.FromAddress != _zeroAddress)
            {
                _context.MemoryCache.Balances.Out(transfer.FromAddress, transfer.TokenAddress, transfer.Amount);
            }
            _context.MemoryCache.Balances.In(transfer.ToAddress, transfer.TokenAddress, transfer.Amount);
        }
    }

    private void MigrateTables()
    {
        using SqliteConnection mainConnection = new($"Data Source={_context.DbLocation}");
        mainConnection.Open();
        mainConnection.Open();

        _context.Logger.Info("SQLite database at: " + _context.DbLocation);

        _context.Logger.Info("Migrating database schema (tables)");
        Schema.MigrateTables(mainConnection);
    }

    private void MigrateIndexes()
    {
        using SqliteConnection mainConnection = new($"Data Source={_context.DbLocation}");
        mainConnection.Open();

        _context.Logger.Info("SQLite database at: " + _context.DbLocation);

        _context.Logger.Info("Migrating database schema (indexes)");
        Schema.MigrateIndexes(mainConnection);
    }

    private void UpdatePathfinder()
    {
        if (Interlocked.Increment(ref _context.PendingPathfinderUpdates) > 1)
        {
            // Already running
            _context.Logger.Info("Pathfinder update already running, skipping ..");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                _context.Logger.Info("Updating pathfinder ..");

                await using FileStream fs = await PathfinderUpdater.ExportToBinaryFile(
                    _context.Settings.PathfinderDbFilePath,
                    _context.MemoryCache);
            }
            catch (Exception e)
            {
                _context.Logger.Error($"Couldn't update the pathfinder at {_context.Settings.PathfinderRpcUrl}", e);
            }
            finally
            {
                Interlocked.Exchange(ref _context.PendingPathfinderUpdates, 0);
                _context.Logger.Info("Updating pathfinder complete ..");
            }
        });
    }

    private long? TryFindReorg()
    {
        _context.Logger.Info("Trying to find reorg.");

        using SqliteConnection mainConnection = new($"Data Source={_context.DbLocation}");
        mainConnection.Open();
        IEnumerable<(long BlockNumber, Keccak BlockHash)> lastPersistedBlocks = Query.LastPersistedBlocks(mainConnection);
        long? reorgAt = null;

        if (_context.NethermindApi.BlockTree == null)
        {
            throw new Exception("BlockTree is null");
        }

        foreach ((long BlockNumber, Keccak BlockHash) recentPersistedBlock in lastPersistedBlocks)
        {
            Block? recentChainBlock = _context.NethermindApi.BlockTree.FindBlock(recentPersistedBlock.BlockNumber);
            if (recentChainBlock == null)
            {
                throw new Exception($"Couldn't find block {recentPersistedBlock.BlockNumber} in the chain");
            }

            if (new Keccak(recentPersistedBlock.BlockHash) == recentChainBlock.Hash)
            {
                continue;
            }

            reorgAt = recentPersistedBlock.BlockNumber;
            break;
        }

        return reorgAt;
    }
}
