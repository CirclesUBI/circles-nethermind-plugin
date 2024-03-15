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

/// <summary>
/// Tracks imported block, their total gas used and the number of transactions and receipts.
/// Allows to get total as well as last 10 sec. averages.
/// </summary>
public class IndexPerformanceMetrics
{
    private long _startPeriodTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private long _totalGasUsed = 0;
    private int _totalTransactions = 0;
    private int _totalBlocks = 0; // Counter for total blocks processed

    private Timer _metricsOutputTimer;

    public IndexPerformanceMetrics()
    {
        // Initialize the timer to call OutputMetrics every 10 seconds
        _metricsOutputTimer = new Timer((e) =>
        {
            var metrics = GetAveragesOverLastPeriod();
            Console.WriteLine(
                $"[Indexer metrics] GasUsed/s: {metrics.GasUsedPerSecond}, Transactions/s: {metrics.TransactionsPerSecond}, Blocks/s: {metrics.BlocksPerSecond}");
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public void LogBlock(Block block)
    {
        var gasUsed = block.Transactions.Sum(tx => tx.GasLimit);
        var transactions = block.Transactions.Length;

        Interlocked.Add(ref _totalGasUsed, gasUsed);
        Interlocked.Add(ref _totalTransactions, transactions);
        Interlocked.Increment(ref _totalBlocks);
    }

    public (double GasUsedPerSecond, double TransactionsPerSecond, double BlocksPerSecond) GetAveragesOverLastPeriod()
    {
        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long elapsedSeconds = currentTimestamp - _startPeriodTimestamp;
        elapsedSeconds = Math.Max(elapsedSeconds, 1); // Prevent division by zero

        // Calculate averages based on the elapsed time since the start of the period
        double gasUsedPerSecond = Interlocked.Read(ref _totalGasUsed) / (double)elapsedSeconds;
        double transactionsPerSecond =
            Interlocked.CompareExchange(ref _totalTransactions, 0, 0) / (double)elapsedSeconds;
        double blocksPerSecond = Interlocked.CompareExchange(ref _totalBlocks, 0, 0) / (double)elapsedSeconds;

        return (GasUsedPerSecond: gasUsedPerSecond, TransactionsPerSecond: transactionsPerSecond,
            BlocksPerSecond: blocksPerSecond);
    }
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
        // return 1;

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
        , int boundedCapacity = -1
        , int parallelism = -1) =>
        new()
        {
            MaxDegreeOfParallelism = parallelism > -1 ? parallelism : GetMaxParallelism(),
            EnsureOrdered = false,
            CancellationToken = cancellationToken,
            BoundedCapacity = boundedCapacity
        };

    private long blocks = 0;

    private void Sink(BlockWithReceipts data)
    {
        visitor.VisitBlock(data.Block);
        Interlocked.Increment(ref blocks);

        if (blocks % 100000 == 0)
        {
            Console.WriteLine($"Imported {blocks} blocks");
        }

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

        _metrics.LogBlock(data.Block);
        visitor.LeaveBlock(data.Block, indexed);
    }

    private Timer _t1;

    private TransformBlock<long, Block> BuildPipeline(CancellationToken cancellationToken)
    {
        TransformBlock<long, Block> blocks = new(
            blockNo => blockTree.FindBlock(blockNo) ?? throw new Exception($"Couldn't find block {blockNo}")
            , CreateOptions(cancellationToken, -1, 1));

        TransformBlock<Block, BlockWithReceipts> receipts = new(
            block => new BlockWithReceipts(block, receiptFinder.Get(block))
            , CreateOptions(cancellationToken, 70000));
        blocks.LinkTo(receipts, new DataflowLinkOptions { PropagateCompletion = true });

        ActionBlock<BlockWithReceipts> sink = new(Sink, CreateOptions(cancellationToken));
        receipts.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        _t1 = new Timer((e) =>
        {
            Console.WriteLine("+----------------+------------+-------------+");
            Console.WriteLine("| Dataflow Block | InputCount | OutputCount |");
            Console.WriteLine("+----------------+------------+-------------+");
            Console.WriteLine($"| blocks         | {blocks.InputCount,10} | {blocks.OutputCount,11} |");
            Console.WriteLine($"| receipts       | {receipts.InputCount,10} | {receipts.OutputCount,11} |");
            Console.WriteLine($"| sink           | {sink.InputCount,10} | {"N/A",11} |");
            Console.WriteLine("+----------------+------------+-------------+");
        }, null, 0, 10000);

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