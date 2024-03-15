using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
using Circles.Index.Utils;
using Microsoft.Data.Sqlite;

namespace Circles.Index.Indexer;

public class StateMachine(
    Context context,
    IIndexer indexer,
    Func<long> getHead,
    Func<long?> tryFindReorg)
{
    public State CurrentState { get; private set; } = State.New;

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

    public async Task HandleEvent(Event e)
    {
        try
        {
            switch (CurrentState)
            {
                case State.New:
                    // Empty state, only used to transition to the initial state
                    return;
                case State.Initial:
                    switch (e)
                    {
                        case Event.EnterState:
                        {
                            // Make sure all tables exist before we start
                            MigrateTables();

                            SetCurrentChainAndIndexHeights();

                            var knownBlocks =
                                StaticResources.GetKnownRelevantBlocks(context.ChainSpec.ChainId);
                            context.KnownBlocks = knownBlocks.KnownBlocks;
                            context.MaxKnownBlock = knownBlocks.MaxKnownBlock;
                            context.MinKnownBlock = knownBlocks.MinKnownBlock;

                            await using SqliteConnection reorgConnection =
                                new($"Data Source={context.IndexDbLocation}");
                            await reorgConnection.OpenAsync();
                            await ReorgHandler.ReorgAt(reorgConnection, context.MemoryCache, context.Logger,
                                Math.Min(context.LastIndexHeight, context.CurrentChainHeight));

                            SetCurrentChainAndIndexHeights();
                            WarmupCache();

                            await TransitionTo(context.CurrentChainHeight == context.LastIndexHeight
                                ? State.WaitForNewBlock
                                : State.Syncing);
                            return;
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
                            return;
                        case Event.SyncCompleted:
                            context.Errors.Clear(); // Clean up errors after a successful sync

                            // TODO: Should only be done once, not every time the event is triggered
                            MigrateIndexes(); // Make sure that the indexes are only created once the syncing is complete
                            UpdatePathfinder();

                            await TransitionTo(State.WaitForNewBlock);
                            return;
                    }

                    break;

                case State.WaitForNewBlock:
                    switch (e)
                    {
                        case Event.NewBlock:
                            if (context.LastReorgAt <= 0)
                            {
                                context.LastReorgAt = tryFindReorg() ?? 0;
                            }

                            if (context.LastReorgAt > 0)
                            {
                                await TransitionTo(State.Reorg);
                                return;
                            }

                            await TransitionTo(State.Syncing);
                            return;
                    }

                    break;

                case State.Reorg:
                    switch (e)
                    {
                        case Event.EnterState:
                            // Internally runs asynchronous, deletes all state after the reorg block and triggers a ReorgCompleted event when done.
                            // After that the method will stop executing and only be restarted once the state is entered again.
                            Reorg();
                            return;
                        case Event.ReorgCompleted:
                            await TransitionTo(State.Syncing);
                            return;
                    }

                    break;

                case State.Error:
                    switch (e)
                    {
                        case Event.EnterState:
                            await TransitionTo(context.Errors.Count >= 3
                                ? State.End
                                : State.Initial);
                            return;
                        case Event.LeaveState:
                            Cleanup();
                            return;
                    }

                    break;

                case State.End:
                    Cleanup();
                    return;
            }

            context.Logger.Debug($"Unhandled event {e} in state {CurrentState}");
        }
        catch (Exception ex)
        {
            context.Logger.Error($"Error while handling {e} event in state {CurrentState}", ex);
            context.Errors.Add(ex);

            await HandleEvent(Event.ErrorOccurred);
        }
    }

    #region Context changing methods

    private void SetCurrentChainAndIndexHeights()
    {
        using SqliteConnection mainConnection = new($"Data Source={context.IndexDbLocation}");
        mainConnection.Open();

        context.LastIndexHeight = Query.LatestBlock(mainConnection) ?? 0;
        context.Logger.Info($"Current index height: {context.LastIndexHeight}");

        context.Logger.Info($"Current chain height: {getHead()}");
    }

    private void Cleanup()
    {
    }

    #endregion

    private async void Reorg()
    {
        context.Logger.Info("Starting reorg process.");
        if (context.LastReorgAt == 0)
        {
            throw new Exception("LastReorgAt is 0");
        }

        await using SqliteConnection connection = new($"Data Source={context.IndexDbLocation}");
        connection.Open();
        await ReorgHandler.ReorgAt(connection, context.MemoryCache, context.Logger, context.LastReorgAt);

        context.LastReorgAt = 0;

        await HandleEvent(Event.ReorgCompleted);
    }

    public async Task TransitionTo(State newState)
    {
        context.Logger.Info($"Transitioning from {CurrentState} to {newState}");
        await HandleEvent(Event.LeaveState);

        CurrentState = newState;

        await HandleEvent(Event.EnterState);
    }

    private async void Sync()
    {
        context.Logger.Info("Starting syncing process.");

        bool success = false;
        try
        {
            IEnumerable<long> blocksToSync = GetBlocksToSync();

            // ReSharper disable once PossibleMultipleEnumeration
            if (blocksToSync.FirstOrDefault(long.MinValue) == long.MinValue)
            {
                context.Logger.Info("No blocks to sync.");
            }
            else
            {
                IndexerVisitor visitor = new();
                Range<long> importedBlocks = await indexer.IndexBlocks(
                    context.MemoryCache,
                    // ReSharper disable once PossibleMultipleEnumeration
                    blocksToSync,
                    visitor,
                    context.CancellationTokenSource.Token);

                context.Logger.Info($"Imported blocks from {importedBlocks.Min} to {importedBlocks.Max}");
            }

            success = true;
        }
        catch (TaskCanceledException)
        {
            context.Logger.Info($"Cancelled indexing blocks.");
        }
        finally
        {
            // TODO: Flush the sink (how???)
            // context.Sink.Flush();
        }

        if (!success)
        {
            return;
        }

        await HandleEvent(Event.SyncCompleted);
    }

    private IEnumerable<long> GetBlocksToSync()
    {
        if (context.LastIndexHeight == context.CurrentChainHeight)
        {
            context.Logger.Info("No blocks to sync.");
            yield break;
        }

        long nextIndexBlock = context.LastIndexHeight == 0 ? 0 : context.LastIndexHeight + 1;
        long from = context.MinKnownBlock > -1 && context.LastIndexHeight <= context.MinKnownBlock
            ? context.MinKnownBlock
            : nextIndexBlock;

        context.Logger.Debug($"Enumerating blocks to sync from {from} to {context.CurrentChainHeight}");

        for (long i = from; i <= context.CurrentChainHeight; i++)
        {
            if (context.KnownBlocks != null
                && i <= context.MaxKnownBlock
                && !context.KnownBlocks.Contains(i))
            {
                continue;
            }

            yield return i;
        }
    }

    private void WarmupCache()
    {
        context.Logger.Info("Warming up cache");
        using SqliteConnection connection = new($"Data Source={context.IndexDbLocation}");
        connection.Open();

        IEnumerable<CirclesSignupDto> signups = Query.CirclesSignups(connection,
            new CirclesSignupQuery { SortOrder = SortOrder.Ascending, Limit = int.MaxValue });
        foreach (CirclesSignupDto signup in signups)
        {
            context.MemoryCache.SignupCache.Add(signup.CirclesAddress, signup.TokenAddress);
        }

        IEnumerable<CirclesTrustDto> trusts = Query.CirclesTrusts(connection,
            new CirclesTrustQuery { SortOrder = SortOrder.Ascending, Limit = int.MaxValue });
        foreach (CirclesTrustDto trust in trusts)
        {
            context.MemoryCache.TrustGraph.AddOrUpdateEdge(trust.UserAddress, trust.CanSendToAddress, trust.Limit);
        }

        // IEnumerable<CirclesTransferDto> transfers = Query.CirclesTransfers(connection, new CirclesTransferQuery { SortOrder = SortOrder.Ascending, Limit = int.MaxValue });
        // foreach (CirclesTransferDto transfer in transfers)
        // {
        //     UInt256 amount = UInt256.Parse(transfer.Amount);
        //     if (transfer.FromAddress != _zeroAddress)
        //     {
        //         _context.MemoryCache.Balances.Out(transfer.FromAddress, transfer.TokenAddress, amount);
        //     }
        //     _context.MemoryCache.Balances.In(transfer.ToAddress, transfer.TokenAddress, amount);
        // }
    }

    private void MigrateTables()
    {
        using SqliteConnection mainConnection = new($"Data Source={context.IndexDbLocation}");
        mainConnection.Open();
        mainConnection.Open();

        context.Logger.Info("SQLite database at: " + context.IndexDbLocation);

        context.Logger.Info("Migrating database schema (tables)");
        Schema.MigrateTables(mainConnection);
    }

    private void MigrateIndexes()
    {
        using SqliteConnection mainConnection = new($"Data Source={context.IndexDbLocation}");
        mainConnection.Open();

        context.Logger.Info("Migrating database schema (indexes)");
        Schema.MigrateIndexes(mainConnection);
    }

    private void UpdatePathfinder()
    {
        if (Interlocked.Increment(ref context.PendingPathfinderUpdates) > 1)
        {
            // Already running
            context.Logger.Info("Pathfinder update already running, skipping ..");
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                context.Logger.Info("Updating pathfinder ..");

                // await using FileStream fs = await PathfinderUpdater.ExportToBinaryFile(
                //     _context.PathfinderDbLocation,
                //     _context.MemoryCache);
                //
                // fs.Close();
                //
                // LibPathfinder.ffi_load_safes_binary(_context.PathfinderDbLocation);
            }
            catch (Exception e)
            {
                context.Logger.Error($"Couldn't update the pathfinder at {context.Settings.PathfinderRpcUrl}", e);
            }
            finally
            {
                Interlocked.Exchange(ref context.PendingPathfinderUpdates, 0);
                context.Logger.Info("Updating pathfinder complete ..");
            }
        });
    }
}