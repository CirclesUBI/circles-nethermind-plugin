using System.Threading.Tasks.Dataflow;
using Circles.Index.Data.Cache;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Circles.Index.Data.Model;

namespace Circles.Index.Indexer;

public interface IIndexerVisitor
{
    void VisitBlock(Block block);

    /// <returns>If the receipt has logs</returns>
    bool VisitReceipt(Block block, TxReceipt receipt);

    /// <returns>If the log entry was used</returns>
    bool VisitLog(Block block, TxReceipt receipt, LogEntry log, int logIndex);

    void LeaveReceipt(Block block, TxReceipt receipt, bool logIndexed);

    void LeaveBlock(Block block, bool receiptIndexed);
}

public interface IIndexer
{
    Task<Range<long>> IndexBlocks(
        MemoryCache cache,
        IEnumerable<long> remainingKnownRelevantBlocks,
        IIndexerVisitor visitor,
        CancellationToken cancellationToken);
}

public record BlockWithReceipts(Block Block, TxReceipt[] Receipts);

public class ImportFlow(
    IBlockTree blockTree,
    IReceiptFinder receiptFinder,
    IIndexerVisitor visitor,
    Settings settings)
{
    private int GetMaxParallelism()
    {
        if (settings.MaxParallelism > 0)
        {
            return settings.MaxParallelism;
        }

        int maxParallelism = Environment.ProcessorCount;
        switch (maxParallelism)
        {
            case >= 3:
                maxParallelism /= 3;
                maxParallelism *= 2;
                return maxParallelism;
            default:
                return 1;
        }
    }

    private ExecutionDataflowBlockOptions CreateOptions(
        CancellationToken cancellationToken
        , bool ordered = true
        , int boundedCapacity = -1) =>
        new()
        {
            MaxDegreeOfParallelism = GetMaxParallelism(),
            EnsureOrdered = ordered,
            CancellationToken = cancellationToken,
            BoundedCapacity = boundedCapacity
        };

    private void Sink(BlockWithReceipts data)
    {
        visitor.VisitBlock(data.Block);

        bool indexed = false;
        foreach (var receipt in data.Receipts)
        {
            visitor.VisitReceipt(data.Block, receipt);
            if (receipt.Logs != null)
            {
                for (int logIndex = 0; logIndex < receipt.Logs.Length; logIndex++)
                {
                    indexed = receipt.Logs.Aggregate(
                        indexed
                        , (p, logEntry) => visitor.VisitLog(data.Block, receipt, logEntry, logIndex) || p);
                }
            }

            visitor.LeaveReceipt(data.Block, receipt, indexed);
        }

        visitor.LeaveBlock(data.Block, indexed);
    }

    private TransformBlock<long, Block> BuildPipeline(CancellationToken cancellationToken)
    {
        TransformBlock<long, Block> blocks = new(
            blockNo => blockTree.FindBlock(blockNo) ?? throw new Exception($"Couldn't find block {blockNo}")
            , CreateOptions(cancellationToken));

        TransformBlock<Block, BlockWithReceipts> receipts = new(
            block => new BlockWithReceipts(block, receiptFinder.Get(block))
            , CreateOptions(cancellationToken));

        blocks.LinkTo(receipts, new DataflowLinkOptions { PropagateCompletion = true });

        ActionBlock<BlockWithReceipts> sink = new(Sink, CreateOptions(cancellationToken));
        receipts.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        return blocks;
    }

    public async Task<Range<long>> Run(IEnumerable<long> blocksToIndex)
    {
        TransformBlock<long, Block> pipeline = BuildPipeline(CancellationToken.None);

        long min = long.MaxValue;
        long max = long.MinValue;

        foreach (long blockNo in blocksToIndex)
        {
            pipeline.Post(blockNo);

            min = Math.Min(min, blockNo);
            max = Math.Max(max, blockNo);
        }

        pipeline.Complete();
        await pipeline.Completion;

        return new Range<long>
        {
            Min = min,
            Max = max
        };
    }
}