using System.Globalization;
using Circles.Index.Indexer.Model;
using Microsoft.Data.Sqlite;
using Nethermind.Int256;

namespace Circles.Index.Data.Sqlite;

public class Sink(string connectionString) : IDisposable, IAsyncDisposable
{
    private readonly InsertBuffer<BlockData> _blockData = new();
    private readonly InsertBuffer<CirclesSignupData> _circlesSignupData = new();
    private readonly InsertBuffer<CirclesTrustData> _circlesTrustData = new();
    private readonly InsertBuffer<CirclesHubTransferData> _circlesHubTransferData = new();
    private readonly InsertBuffer<Erc20TransferData> _erc20TransferData = new();

    private SqliteTransaction? _flushTransaction;

    public void AddBlock(long blockNumber, ulong timestamp, string blockHash)
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
            using var flushConnection = new SqliteConnection(connectionString);
            flushConnection.Open();
            using var flushTransaction = flushConnection.BeginTransaction();

            FlushBlocks(flushConnection, flushTransaction);
            FlushCirclesSignups(flushConnection, flushTransaction);
            FlushCirclesTrusts(flushConnection, flushTransaction);
            FlushCirclesHubTransfers(flushConnection, flushTransaction);
            FlushErc20Transfers(flushConnection, flushTransaction);

            flushTransaction.Commit();
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

    SqliteCommand? _flushBlocksCommand;

    private void PrepareFlushBlocksCommand(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        _flushBlocksCommand = flushConnection.CreateCommand();
        _flushBlocksCommand.Transaction = flushTransaction;
        _flushBlocksCommand.CommandText =
            $"INSERT INTO {TableNames.Block} (block_number, timestamp, block_hash) VALUES (@blockNumber, @timestamp, @blockHash)";
        _flushBlocksCommand.Parameters.Add("@blockNumber", SqliteType.Integer);
        _flushBlocksCommand.Parameters.Add("@timestamp", SqliteType.Integer);
        _flushBlocksCommand.Parameters.Add("@blockHash", SqliteType.Text);
        _flushBlocksCommand.Prepare();
    }

    private void FlushBlocks(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        PrepareFlushBlocksCommand(flushConnection, flushTransaction);
        foreach (var item in _blockData.TakeSnapshot())
        {
            _flushBlocksCommand!.Parameters["@blockNumber"].Value = item.BlockNumber;
            _flushBlocksCommand!.Parameters["@timestamp"].Value = item.Timestamp;
            _flushBlocksCommand!.Parameters["@blockHash"].Value = item.BlockHash;
            _flushBlocksCommand!.ExecuteNonQuery();
        }

        _flushBlocksCommand!.Dispose();
    }

    SqliteCommand? _flushCirclesSignupCommand;

    private void PrepareFlushCirclesSignups(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        _flushCirclesSignupCommand = flushConnection.CreateCommand();
        _flushCirclesSignupCommand.Transaction = flushTransaction;
        _flushCirclesSignupCommand.CommandText =
            $"INSERT INTO {TableNames.CirclesSignup} (block_number, timestamp, transaction_index, log_index, transaction_hash, circles_address, token_address) VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @circlesAddress, @tokenAddress)";
        _flushCirclesSignupCommand.Parameters.Add("@blockNumber", SqliteType.Integer);
        _flushCirclesSignupCommand.Parameters.Add("@timestamp", SqliteType.Integer);
        _flushCirclesSignupCommand.Parameters.Add("@transaction_index", SqliteType.Integer);
        _flushCirclesSignupCommand.Parameters.Add("@log_index", SqliteType.Integer);
        _flushCirclesSignupCommand.Parameters.Add("@transactionHash", SqliteType.Text);
        _flushCirclesSignupCommand.Parameters.Add("@circlesAddress", SqliteType.Text);
        _flushCirclesSignupCommand.Parameters.Add("@tokenAddress", SqliteType.Text);
        _flushCirclesSignupCommand.Prepare();
    }

    private void FlushCirclesSignups(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        PrepareFlushCirclesSignups(flushConnection, flushTransaction);
        foreach (var item in _circlesSignupData.TakeSnapshot())
        {
            _flushCirclesSignupCommand!.Parameters["@blockNumber"].Value = item.BlockNumber;
            _flushCirclesSignupCommand!.Parameters["@timestamp"].Value = item.Timestamp;
            _flushCirclesSignupCommand!.Parameters["@transaction_index"].Value = item.TransactionIndex;
            _flushCirclesSignupCommand!.Parameters["@log_index"].Value = item.LogIndex;
            _flushCirclesSignupCommand!.Parameters["@transactionHash"].Value = item.TransactionHash;
            _flushCirclesSignupCommand!.Parameters["@circlesAddress"].Value = item.CirclesAddress;
            _flushCirclesSignupCommand!.Parameters["@tokenAddress"].Value = (object?)item.TokenAddress ?? DBNull.Value;
            _flushCirclesSignupCommand!.ExecuteNonQuery();
        }

        _flushCirclesSignupCommand!.Dispose();
    }

    SqliteCommand? _flushCirclesTrustCommand;

    private void PrepareFlushCirclesTrusts(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        _flushCirclesTrustCommand = flushConnection.CreateCommand();
        _flushCirclesTrustCommand.Transaction = flushTransaction;
        _flushCirclesTrustCommand.CommandText =
            $"INSERT INTO {TableNames.CirclesTrust} (block_number, timestamp, transaction_index, log_index, transaction_hash, user_address, can_send_to_address, \"limit\") VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @userAddress, @canSendToAddress, @limit)";
        _flushCirclesTrustCommand.Parameters.Add("@blockNumber", SqliteType.Integer);
        _flushCirclesTrustCommand.Parameters.Add("@timestamp", SqliteType.Integer);
        _flushCirclesTrustCommand.Parameters.Add("@transaction_index", SqliteType.Integer);
        _flushCirclesTrustCommand.Parameters.Add("@log_index", SqliteType.Integer);
        _flushCirclesTrustCommand.Parameters.Add("@transactionHash", SqliteType.Text);
        _flushCirclesTrustCommand.Parameters.Add("@userAddress", SqliteType.Text);
        _flushCirclesTrustCommand.Parameters.Add("@canSendToAddress", SqliteType.Text);
        _flushCirclesTrustCommand.Parameters.Add("@limit", SqliteType.Integer);
        _flushCirclesTrustCommand.Prepare();
    }

    private void FlushCirclesTrusts(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        PrepareFlushCirclesTrusts(flushConnection, flushTransaction);
        foreach (var item in _circlesTrustData.TakeSnapshot())
        {
            _flushCirclesTrustCommand!.Parameters["@blockNumber"].Value = item.BlockNumber;
            _flushCirclesTrustCommand!.Parameters["@timestamp"].Value = item.Timestamp;
            _flushCirclesTrustCommand!.Parameters["@transaction_index"].Value = item.TransactionIndex;
            _flushCirclesTrustCommand!.Parameters["@log_index"].Value = item.LogIndex;
            _flushCirclesTrustCommand!.Parameters["@transactionHash"].Value = item.TransactionHash;
            _flushCirclesTrustCommand!.Parameters["@userAddress"].Value = item.UserAddress;
            _flushCirclesTrustCommand!.Parameters["@canSendToAddress"].Value = item.CanSendToAddress;
            _flushCirclesTrustCommand!.Parameters["@limit"].Value = item.Limit;
            _flushCirclesTrustCommand!.ExecuteNonQuery();
        }

        _flushCirclesTrustCommand!.Dispose();
    }

    SqliteCommand? _flushCirclesHubTransferCommand;

    private void PrepareFlushCirclesHubTransfers(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        _flushCirclesHubTransferCommand = flushConnection.CreateCommand();
        _flushCirclesHubTransferCommand.Transaction = flushTransaction;
        _flushCirclesHubTransferCommand.CommandText =
            $"INSERT INTO {TableNames.CirclesHubTransfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, from_address, to_address, amount) VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @fromAddress, @toAddress, @amount)";
        _flushCirclesHubTransferCommand.Parameters.Add("@blockNumber", SqliteType.Integer);
        _flushCirclesHubTransferCommand.Parameters.Add("@timestamp", SqliteType.Integer);
        _flushCirclesHubTransferCommand.Parameters.Add("@transaction_index", SqliteType.Integer);
        _flushCirclesHubTransferCommand.Parameters.Add("@log_index", SqliteType.Integer);
        _flushCirclesHubTransferCommand.Parameters.Add("@transactionHash", SqliteType.Text);
        _flushCirclesHubTransferCommand.Parameters.Add("@fromAddress", SqliteType.Text);
        _flushCirclesHubTransferCommand.Parameters.Add("@toAddress", SqliteType.Text);
        _flushCirclesHubTransferCommand.Parameters.Add("@amount", SqliteType.Text);
        _flushCirclesHubTransferCommand.Prepare();
    }

    private void FlushCirclesHubTransfers(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        PrepareFlushCirclesHubTransfers(flushConnection, flushTransaction);
        foreach (var item in _circlesHubTransferData.TakeSnapshot())
        {
            _flushCirclesHubTransferCommand!.Parameters["@blockNumber"].Value = item.BlockNumber;
            _flushCirclesHubTransferCommand!.Parameters["@timestamp"].Value = item.Timestamp;
            _flushCirclesHubTransferCommand!.Parameters["@transaction_index"].Value = item.TransactionIndex;
            _flushCirclesHubTransferCommand!.Parameters["@log_index"].Value = item.LogIndex;
            _flushCirclesHubTransferCommand!.Parameters["@transactionHash"].Value = item.TransactionHash;
            _flushCirclesHubTransferCommand!.Parameters["@fromAddress"].Value = item.FromAddress;
            _flushCirclesHubTransferCommand!.Parameters["@toAddress"].Value = item.ToAddress;
            _flushCirclesHubTransferCommand!.Parameters["@amount"].Value =
                item.Amount.ToString(CultureInfo.InvariantCulture);
            _flushCirclesHubTransferCommand!.ExecuteNonQuery();
        }

        _flushCirclesHubTransferCommand!.Dispose();
    }

    SqliteCommand? _flushErc20TransferCommand;

    private void PrepareFlushErc20Transfers(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        _flushErc20TransferCommand = flushConnection.CreateCommand();
        _flushErc20TransferCommand.Transaction = flushTransaction;
        _flushErc20TransferCommand.CommandText =
            $"INSERT INTO {TableNames.Erc20Transfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, token_address, from_address, to_address, amount) VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @tokenAddress, @fromAddress, @toAddress, @amount)";
        _flushErc20TransferCommand.Parameters.Add("@blockNumber", SqliteType.Integer);
        _flushErc20TransferCommand.Parameters.Add("@timestamp", SqliteType.Integer);
        _flushErc20TransferCommand.Parameters.Add("@transaction_index", SqliteType.Integer);
        _flushErc20TransferCommand.Parameters.Add("@log_index", SqliteType.Integer);
        _flushErc20TransferCommand.Parameters.Add("@transactionHash", SqliteType.Text);
        _flushErc20TransferCommand.Parameters.Add("@tokenAddress", SqliteType.Text);
        _flushErc20TransferCommand.Parameters.Add("@fromAddress", SqliteType.Text);
        _flushErc20TransferCommand.Parameters.Add("@toAddress", SqliteType.Text);
        _flushErc20TransferCommand.Parameters.Add("@amount", SqliteType.Text);
        _flushErc20TransferCommand.Prepare();
    }

    private void FlushErc20Transfers(SqliteConnection flushConnection, SqliteTransaction flushTransaction)
    {
        PrepareFlushErc20Transfers(flushConnection, flushTransaction);
        foreach (var item in _erc20TransferData.TakeSnapshot())
        {
            _flushErc20TransferCommand!.Parameters["@blockNumber"].Value = item.BlockNumber;
            _flushErc20TransferCommand!.Parameters["@timestamp"].Value = item.Timestamp;
            _flushErc20TransferCommand!.Parameters["@transaction_index"].Value = item.TransactionIndex;
            _flushErc20TransferCommand!.Parameters["@log_index"].Value = item.LogIndex;
            _flushErc20TransferCommand!.Parameters["@transactionHash"].Value = item.TransactionHash;
            _flushErc20TransferCommand!.Parameters["@tokenAddress"].Value = item.TokenAddress;
            _flushErc20TransferCommand!.Parameters["@fromAddress"].Value = item.FromAddress;
            _flushErc20TransferCommand!.Parameters["@toAddress"].Value = item.ToAddress;
            _flushErc20TransferCommand!.Parameters["@amount"].Value =
                item.Amount.ToString(CultureInfo.InvariantCulture);
            _flushErc20TransferCommand!.ExecuteNonQuery();
        }

        _flushErc20TransferCommand!.Dispose();
    }
}