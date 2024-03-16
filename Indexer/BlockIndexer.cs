using System.Threading.Tasks.Dataflow;
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

public record BlockWithReceipts(Block Block, TxReceipt[] Receipts);

public class ImportFlow(
    IBlockTree blockTree,
    IReceiptFinder receiptFinder,
    IIndexerVisitor visitor,
    Settings settings)
{
    private IndexPerformanceMetrics _metrics = new();

    private int GetMaxParallelism()
    {
        if (settings.MaxParallelism > 0)
        {
            return settings.MaxParallelism;
        }

        switch (Environment.ProcessorCount)
        {
            case >= 32: return 24; // 8
            case >= 24: return 18; // 6
            case >= 20: return 15; // 5
            case >= 16: return 12; // 4
            case >= 12: return 9; // 3
            case >= 8: return 6; // 2
            case >= 4: return 3; // 1
            case >= 3: return 2; // 1
            default: return 1;
        }
    }

    private ExecutionDataflowBlockOptions CreateOptions(
        CancellationToken cancellationToken
        , int boundedCapacity = -1
        , int parallelism = -1) =>
        new()
        {
            MaxDegreeOfParallelism = parallelism > -1 ? parallelism : GetMaxParallelism(),
            EnsureOrdered = true,
            CancellationToken = cancellationToken,
            BoundedCapacity = boundedCapacity
        };

    private long _totalBlocks = 0;

    private void Sink(BlockWithReceipts data)
    {
        visitor.VisitBlock(data.Block);
        Interlocked.Increment(ref _totalBlocks);

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

        _metrics.LogBlockWithReceipts(data);
        visitor.LeaveBlock(data.Block, indexed);
    }

    private TransformBlock<long, Block> BuildPipeline(CancellationToken cancellationToken)
    {
        TransformBlock<long, Block> blocks = new(
            blockNo => blockTree.FindBlock(blockNo) ?? throw new Exception($"Couldn't find block {blockNo}")
            , CreateOptions(cancellationToken, 1, 1));

        TransformBlock<Block, BlockWithReceipts> receipts = new(
            block => new BlockWithReceipts(block, receiptFinder.Get(block))
            , CreateOptions(cancellationToken, Environment.ProcessorCount * 4));
        blocks.LinkTo(receipts);

        ActionBlock<BlockWithReceipts> sink = new(Sink,
            CreateOptions(cancellationToken, 1));
        receipts.LinkTo(sink);

        // _showPerformanceMetricsTimer = new Timer((e) =>
        // {
        //     Console.WriteLine("+----------------+------------+-------------+");
        //     Console.WriteLine("| Dataflow Block | InputCount | OutputCount |");
        //     Console.WriteLine("+----------------+------------+-------------+");
        //     Console.WriteLine($"| blocks         | {blocks.InputCount,10} | {blocks.OutputCount,11} |");
        //     Console.WriteLine($"| receipts       | {receipts.InputCount,10} | {receipts.OutputCount,11} |");
        //     Console.WriteLine($"| sink           | {sink.InputCount,10} | {"N/A",11} |");
        //     Console.WriteLine("+----------------+------------+-------------+");
        // }, null, 0, 10000);

        return blocks;
    }

    public async Task<Range<long>> Run(IAsyncEnumerable<long> blocksToIndex, CancellationToken? cancellationToken)
    {
        TransformBlock<long, Block> pipeline = BuildPipeline(CancellationToken.None);

        long min = long.MaxValue;
        long max = long.MinValue;

        if (cancellationToken == null)
        {
            CancellationTokenSource cts = new();
            cancellationToken = cts.Token;
        }

        var source = blocksToIndex.WithCancellation(cancellationToken.Value);

        await foreach (var blockNo in source)
        {
            await pipeline.SendAsync(blockNo, cancellationToken.Value);

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