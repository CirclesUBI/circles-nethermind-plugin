using System.Globalization;
using System.Numerics;
using Circles.Index.Data.Sqlite;
using Circles.Index.Indexer.Model;
using Nethermind.Evm.Tracing.GethStyle.JavaScript;
using Npgsql;
using Nethermind.Int256;
using NpgsqlTypes;

namespace Circles.Index.Data.Postgresql;

public class Sink : IDisposable, IAsyncDisposable
{
    private readonly InsertBuffer<BlockData> _blockData = new();
    private readonly InsertBuffer<CirclesSignupData> _circlesSignupData = new();
    private readonly InsertBuffer<CirclesTrustData> _circlesTrustData = new();
    private readonly InsertBuffer<CirclesHubTransferData> _circlesHubTransferData = new();
    private readonly InsertBuffer<Erc20TransferData> _erc20TransferData = new();

    private readonly string _connectionString;

    public Sink(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void AddBlock(long blockNumber, long timestamp, string blockHash)
    {
        _blockData.Add(new(blockNumber, timestamp, blockHash));
    }

    public void AddCirclesSignup(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,
        string transactionHash, string circlesAddress, string? tokenAddress)
    {
        _circlesSignupData.Add(new CirclesSignupData(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, circlesAddress,
            tokenAddress));
    }

    public void AddCirclesTrust(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,
        string transactionHash, string userAddress, string canSendToAddress, int limit)
    {
        _circlesTrustData.Add(new(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, userAddress,
            canSendToAddress, limit));
    }

    public void AddCirclesHubTransfer(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,
        string transactionHash, string fromAddress, string toAddress, UInt256 amount)
    {
        _circlesHubTransferData.Add(new(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, fromAddress,
            toAddress, amount
        ));
    }

    public void AddErc20Transfer(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,
        string transactionHash, string tokenAddress, string from, string to, UInt256 value)
    {
        _erc20TransferData.Add(new(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, tokenAddress, from,
            to, value
        ));
    }

    public void Flush()
    {
        try
        {
            using var flushConnection = new NpgsqlConnection(_connectionString);
            flushConnection.Open();

            FlushBlocksBulk(flushConnection);
            FlushCirclesSignupsBulk(flushConnection);
            FlushCirclesTrustsBulk(flushConnection);
            FlushCirclesHubTransfersBulk(flushConnection);
            FlushErc20TransfersBulk(flushConnection);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void Dispose()
    {
        Flush();
    }

    public async ValueTask DisposeAsync()
    {
        Flush();
    }

    private void FlushBlocksBulk(NpgsqlConnection flushConnection)
    {
        using var writer = flushConnection.BeginBinaryImport($"COPY {TableNames.Block} (block_number, timestamp, block_hash) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _blockData.TakeSnapshot())
        {
            writer.StartRow();
            writer.Write(item.BlockNumber, NpgsqlDbType.Bigint);
            writer.Write(item.Timestamp, NpgsqlDbType.Bigint);
            writer.Write(item.BlockHash, NpgsqlDbType.Text);
        }
        writer.Complete();
    }

        private void FlushCirclesSignupsBulk(NpgsqlConnection flushConnection)
    {
        using var writer = flushConnection.BeginBinaryImport($"COPY {TableNames.CirclesSignup} (block_number, timestamp, transaction_index, log_index, transaction_hash, circles_address, token_address) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _circlesSignupData.TakeSnapshot())
        {
            writer.StartRow();
            writer.Write(item.BlockNumber, NpgsqlDbType.Bigint);
            writer.Write((long)item.Timestamp, NpgsqlDbType.Bigint);
            writer.Write(item.TransactionIndex, NpgsqlDbType.Integer);
            writer.Write(item.LogIndex, NpgsqlDbType.Integer);
            writer.Write(item.TransactionHash, NpgsqlDbType.Text);
            writer.Write(item.CirclesAddress, NpgsqlDbType.Text);
            writer.Write(item.TokenAddress ?? string.Empty, NpgsqlDbType.Text);
        }
        writer.Complete();
    }

    private void FlushCirclesTrustsBulk(NpgsqlConnection flushConnection)
    {
        using var writer = flushConnection.BeginBinaryImport(
            $"COPY {TableNames.CirclesTrust} (" +
            $"block_number" +
            $", timestamp" +
            $", transaction_index" +
            $", log_index" +
            $", transaction_hash" +
            $", user_address" +
            $", can_send_to_address" +
            $", \"limit\") FROM STDIN (FORMAT BINARY)");
        
        foreach (var item in _circlesTrustData.TakeSnapshot())
        {
            writer.StartRow();
            writer.Write(item.BlockNumber, NpgsqlDbType.Bigint);
            writer.Write((long)item.Timestamp, NpgsqlDbType.Bigint);
            writer.Write(item.TransactionIndex, NpgsqlDbType.Integer);
            writer.Write(item.LogIndex, NpgsqlDbType.Integer);
            writer.Write(item.TransactionHash, NpgsqlDbType.Text);
            writer.Write(item.UserAddress, NpgsqlDbType.Text);
            writer.Write(item.CanSendToAddress, NpgsqlDbType.Text);
            writer.Write((long)item.Limit, NpgsqlDbType.Bigint);
        }
        writer.Complete();
    }

    private void FlushCirclesHubTransfersBulk(NpgsqlConnection flushConnection)
    {
        using var writer = flushConnection.BeginBinaryImport($"COPY {TableNames.CirclesHubTransfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, from_address, to_address, amount) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _circlesHubTransferData.TakeSnapshot())
        {
            writer.StartRow();
            writer.Write(item.BlockNumber, NpgsqlDbType.Bigint);
            writer.Write((long)item.Timestamp, NpgsqlDbType.Bigint);
            writer.Write(item.TransactionIndex, NpgsqlDbType.Integer);
            writer.Write(item.LogIndex, NpgsqlDbType.Integer);
            writer.Write(item.TransactionHash, NpgsqlDbType.Text);
            writer.Write(item.FromAddress, NpgsqlDbType.Text);
            writer.Write(item.ToAddress, NpgsqlDbType.Text);
            writer.Write((BigInteger)item.Amount, NpgsqlDbType.Numeric);
        }
        writer.Complete();
    }

    private void FlushErc20TransfersBulk(NpgsqlConnection flushConnection)
    {
        using var writer = flushConnection.BeginBinaryImport($"COPY {TableNames.Erc20Transfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, token_address, from_address, to_address, amount) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _erc20TransferData.TakeSnapshot())
        {
            writer.StartRow();
            writer.Write(item.BlockNumber, NpgsqlDbType.Bigint);
            writer.Write((long)item.Timestamp, NpgsqlDbType.Bigint);
            writer.Write(item.TransactionIndex, NpgsqlDbType.Integer);
            writer.Write(item.LogIndex, NpgsqlDbType.Integer);
            writer.Write(item.TransactionHash, NpgsqlDbType.Text);
            writer.Write(item.TokenAddress, NpgsqlDbType.Text);
            writer.Write(item.FromAddress, NpgsqlDbType.Text);
            writer.Write(item.ToAddress, NpgsqlDbType.Text);
            writer.Write((BigInteger)item.Amount, NpgsqlDbType.Numeric);
        }
        writer.Complete();
    }
}