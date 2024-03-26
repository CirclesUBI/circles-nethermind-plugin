using System.Threading.Tasks.Dataflow;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
using Nethermind.Core.Crypto;

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
    Sink dataSink)
{
    private static readonly IndexPerformanceMetrics _metrics = new();

    private ExecutionDataflowBlockOptions CreateOptions(
        CancellationToken cancellationToken
        , int boundedCapacity = -1
        , int parallelism = -1) =>
        new()
        {
            MaxDegreeOfParallelism = parallelism > -1 ? parallelism : Environment.ProcessorCount,
            EnsureOrdered = false,
            CancellationToken = cancellationToken,
            BoundedCapacity = boundedCapacity
        };


    private BlockWithReceipts Sink(BlockWithReceipts data)
    {
        try
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
                        var logEntry = receipt.Logs[logIndex];
                        visitor.VisitLog(data.Block, receipt, logEntry, logIndex);
                    }
                }

                visitor.LeaveReceipt(data.Block, receipt, indexed);
            }

            visitor.LeaveBlock(data.Block, indexed);
            _metrics.LogBlockWithReceipts(data);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return data;
    }

    private TransformBlock<long, Block> BuildPipeline(CancellationToken cancellationToken)
    {
        var flushIntervalInBlocks = 100000;

        BlockHeader generateDummyBlockHeader(long blockNo)
        {
            var dummyHash = Keccak.Compute(new[] { (byte)blockNo });
            var dummyAddress = Address.Zero;
            return new(dummyHash, dummyHash, dummyAddress, 0, blockNo, 0, 0, Array.Empty<byte>(), 0, 0, dummyHash);
        }

        TransformBlock<long, Block> blocks = new(
            blockNo =>
            {
                return blockNo < Caches.MaxKnownBlock && !Caches.KnownBlocks.ContainsKey(blockNo)
                    ? new Block(generateDummyBlockHeader(blockNo))
                    : (blockTree.FindBlock(blockNo) ?? throw new Exception($"Couldn't find block {blockNo}"));
            }
            , CreateOptions(cancellationToken, 4));

        TransformBlock<Block, BlockWithReceipts> receipts = new(
            block => new BlockWithReceipts(
                block
                , block.Number < Caches.MaxKnownBlock && !Caches.KnownBlocks.ContainsKey(block.Number)
                    ? Array.Empty<TxReceipt>()
                    : receiptFinder.Get(block))
            , CreateOptions(cancellationToken, 8));
        blocks.LinkTo(receipts);

        TransformBlock<BlockWithReceipts, BlockWithReceipts> sink = new(Sink,
            CreateOptions(cancellationToken, 16));
        receipts.LinkTo(sink);

        long accumulated = 0;
        ActionBlock<BlockWithReceipts> flush = new(block =>
            {
                if (accumulated >= flushIntervalInBlocks)
                {
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    dataSink.Flush();
                    accumulated = 0;

                    var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;
                    Console.WriteLine($"Flushed {flushIntervalInBlocks} blocks in {elapsed}ms");
                }
                else
                {
                    accumulated++;
                }
            },
            CreateOptions(cancellationToken, flushIntervalInBlocks * 5, 1));
        sink.LinkTo(flush);

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

        dataSink.Flush();

        return new Range<long>
        {
            Min = min,
            Max = max
        };
    }
}