using System.Collections.Immutable;
using System.Threading.Tasks.Dataflow;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;

namespace Circles.Index;

public class CirclesIndex : INethermindPlugin
{
    public string Name => "Circles.Index";

    public string Description =>
        "Indexes Circles related events and provides query capabilities via JSON-RPC. Indexed events are: Signup, OrgSignup, Trust, HubTransfer, CrcTransfer.";

    public string Author => "Gnosis";

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private SqlitePersistence? _persistence;
    private INethermindApi? _nethermindApi;

    public Task Init(INethermindApi nethermindApi)
    {
        _nethermindApi = nethermindApi;

        ILogger baseLogger = nethermindApi.LogManager.GetClassLogger();
        ILogger pluginLogger = new LoggerWithPrefix("Circles.Index:", baseLogger);
        IInitConfig initConfig = nethermindApi.Config<IInitConfig>();

        pluginLogger.Debug("Initializing persistence ..");
        _persistence =
            new SqlitePersistence(Path.Combine(initConfig.BaseDbPath, Settings.DbFileName), 10000, pluginLogger);
        _persistence.Initialize();

        ulong chainId = nethermindApi.ChainSpec.ChainId;
        pluginLogger.Debug($"Loading known relevant blocks for chain-id {chainId} ..");
        (ImmutableHashSet<long> allKnownRelevantBlocks, long maxKnownRelevantBlock) =
            StaticResources.GetKnownRelevantBlocks(chainId);
        pluginLogger.Info($"Max known relevant block: {maxKnownRelevantBlock}");

        bool isWorking = false;
        long? reorgAt = null;

        nethermindApi.BlockTree!.NewHeadBlock += (_, e) =>
        {
            long lastPersistedBlock = _persistence.GetLastPersistedBlock();
            if (e.Block.Number <= lastPersistedBlock)
            {
                pluginLogger.Warn(
                    $"Circles.Index needs to be re-built from block {e.Block.Number} because of a reorg.");
                reorgAt = e.Block.Number;
            }

            if (isWorking)
            {
                return;
            }

            isWorking = true;

#pragma warning disable CS4014
            Task.Run(async () =>
#pragma warning restore CS4014
            {
                try
                {
                    if (reorgAt != null)
                    {
                        _persistence.DeleteFrom(reorgAt.Value);
                        reorgAt = null;
                    }

                    pluginLogger.Debug("Querying last persisted relevant block ..");
                    long lastPersistedRelevantBlock = _persistence.GetLastPersistedBlock();
                    pluginLogger.Info($"Last persisted relevant block: {lastPersistedRelevantBlock}");

                    (IBlockTree blockTree, long latestBlock) = await GetBlockTree(nethermindApi);
                    pluginLogger.Info($"Current block tree head: {latestBlock}");

                    IReceiptFinder receiptFinder = nethermindApi.ReceiptFinder!;

                    IEnumerable<long> blocksToIndex = GetBlocksToIndex(
                        pluginLogger
                        , _cancellationTokenSource.Token
                        , lastPersistedRelevantBlock
                        , allKnownRelevantBlocks
                        , maxKnownRelevantBlock
                        , latestBlock);

                    await IndexBlocks(
                        blockTree
                        , receiptFinder
                        , _persistence
                        , blocksToIndex
                        , _cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    pluginLogger.Error("Error while indexing blocks.", ex);
                }
                finally
                {
                    _persistence.Flush();
                    isWorking = false;
                }
            }, _cancellationTokenSource.Token);
        };

        return Task.CompletedTask;
    }

    private static IEnumerable<long> GetBlocksToIndex(
        ILogger pluginLogger
        , CancellationToken cancellationToken
        , long lastPersistedRelevantBlock
        , ImmutableHashSet<long> knownRelevantBlocks
        , long maxKnownRelevantBlock
        , long latestBlock)
    {
        // Determine which blocks to index:
        // 1. All blocks that are known to be relevant but not yet indexed.
        // 2. All blocks up to the chain's current head.
        long from = lastPersistedRelevantBlock + 1;
        if (latestBlock >= maxKnownRelevantBlock && from <= maxKnownRelevantBlock)
        {
            pluginLogger.Info($"Indexing known relevant blocks {from} to {maxKnownRelevantBlock} ..");
            foreach (long blockNo in knownRelevantBlocks.Where(o => o > lastPersistedRelevantBlock))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                yield return blockNo;
            }
        }
        else if (from <= latestBlock)
        {
            pluginLogger.Info($"Indexing new blocks {from} to {latestBlock} ..");
            for (long blockNo = from; blockNo <= latestBlock; blockNo++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                yield return blockNo;
            }
        }
        else
        {
            pluginLogger.Info("No new blocks to index.");
        }
    }

    private static async Task IndexBlocks(
        IBlockTree blockTree,
        IReceiptFinder receiptFinder,
        SqlitePersistence persistence,
        IEnumerable<long> remainingKnownRelevantBlocks,
        CancellationToken cancellationToken)
    {
        int maxParallelism = Environment.ProcessorCount;
        switch (maxParallelism)
        {
            case >= 3:
                maxParallelism /= 3;
                maxParallelism *= 2;
                break;
            case 2:
                maxParallelism /= 2;
                break;
            default:
                maxParallelism = 1;
                break;
        }

        TransformBlock<long, (long blockNo, TxReceipt[] receipts)> getReceiptsBlock = new(
            blockNo => (blockNo, FindBlockReceipts(blockTree, receiptFinder, blockNo))
            , new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxParallelism, EnsureOrdered = true, CancellationToken = cancellationToken });

        ActionBlock<(long blockNo, TxReceipt[] receipts)> indexReceiptsBlock = new(data => {
                HashSet<long> relevantBlocks = Indexer.IndexReceipts(data.receipts, persistence);
                foreach (long relevantBlock in relevantBlocks)
                {
                    persistence.AddRelevantBlock(relevantBlock);
                }

                if (!relevantBlocks.Contains(data.blockNo))
                {
                    persistence.AddIrrelevantBlock(data.blockNo);
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 1,
                EnsureOrdered = true,
                CancellationToken = cancellationToken
            });

        getReceiptsBlock.LinkTo(indexReceiptsBlock, new DataflowLinkOptions { PropagateCompletion = true });

        foreach (long blockNo in remainingKnownRelevantBlocks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            getReceiptsBlock.Post(blockNo);
        }

        getReceiptsBlock.Complete();

        await indexReceiptsBlock.Completion;
    }

    private static TxReceipt[] FindBlockReceipts(IBlockTree blockTree, IReceiptFinder receiptFinder, long blockNo)
    {
        Block? block = blockTree.FindBlock(blockNo);
        if (block == null)
        {
            return Array.Empty<TxReceipt>();
        }

        return receiptFinder.Get(block);
    }

    private static async Task<(IBlockTree blockTree, long latestBlock)> GetBlockTree(INethermindApi nethermindApi)
    {
        long? to = nethermindApi.BlockTree?.Head?.Number;

        while (to is null or 0)
        {
            await Task.Delay(1000);
            to = nethermindApi.BlockTree!.Head!.Number;
        }

        return (nethermindApi.BlockTree!, to.Value);
    }

    #region Default implementation

    public Task InitNetworkProtocol()
    {
        return Task.CompletedTask;
    }


    public Task InitRpcModules()
    {
        if (_nethermindApi == null)
        {
            throw new Exception("_nethermindApi is not set");
        }

        (IApiWithNetwork apiWithNetwork, _) =  _nethermindApi.ForRpc;
        apiWithNetwork.RpcModuleProvider?.Register(new SingletonModulePool<ICirclesRpcModule>(new CirclesRpcModule(_nethermindApi)));

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        if (_persistence != null)
        {
            await _persistence.DisposeAsync();
        }
    }

    #endregion
}
