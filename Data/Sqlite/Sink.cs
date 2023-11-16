using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Circles.Index.Data.Sqlite;

/// <summary>
/// Writes Circles events to a SQLite database.
/// </summary>
public class Sink
{
    private readonly SqliteConnection _connection;
    private readonly int _transactionLimit;

    private SqliteTransaction? _transaction;
    private int _transactionCounter;

    private SqliteCommand? _addRelevantBlockInsertCmd;
    private SqliteCommand? _addIrrelevantBlockInsertCmd;
    private SqliteCommand? _addCirclesSignupInsertCmd;
    private SqliteCommand? _addCirclesTrustInsertCmd;
    private SqliteCommand? _addCirclesHubTransferInsertCmd;
    private SqliteCommand? _addCirclesTransferInsertCmd;

    private long _blocksProcessedCounter;

    private readonly ILogger? _logger;

    private readonly Stopwatch _stopwatch = new();

    /// <summary>
    /// Creates a new instance of <see cref="Sink"/>.
    /// </summary>
    /// <param name="connection">An open and writable sqlite connection</param>
    /// <param name="insertBatchSize">The number of inserts to batch into a single transaction</param>
    /// <param name="logger">The logger to use</param>
    public Sink(SqliteConnection connection, int insertBatchSize, ILogger? logger)
    {
        if (insertBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(insertBatchSize), "Insert batch size must be greater than 0.");
        }

        _logger = logger;
        _connection = connection;
        _transactionLimit = insertBatchSize;
        _stopwatch.Start();
    }

    public void AddRelevantBlock(long blockNumber, ulong timestamp, string blockHash)
    {
        PrepareAddRelevantBlockInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addRelevantBlockInsertCmd!.Transaction = _transaction;
        _addRelevantBlockInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addRelevantBlockInsertCmd.Parameters["@timestamp"].Value = timestamp;
        _addRelevantBlockInsertCmd.Parameters["@blockHash"].Value = blockHash;
        _addRelevantBlockInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            // Only commit when a block was added (which happens last in the import process)
            CommitTransaction();
        }

        _blocksProcessedCounter++;
        LogBlockThroughput();
    }

    public void AddIrrelevantBlock(long blockNumber, ulong timestamp, string blockHash)
    {
        PrepareAddIrrelevantBlockInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addIrrelevantBlockInsertCmd!.Transaction = _transaction;
        _addIrrelevantBlockInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addIrrelevantBlockInsertCmd.Parameters["@timestamp"].Value = timestamp;
        _addIrrelevantBlockInsertCmd.Parameters["@blockHash"].Value = blockHash;
        _addIrrelevantBlockInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            // Only commit when a block was added (which happens last in the import process)
            CommitTransaction();
        }

        _blocksProcessedCounter++;
        LogBlockThroughput();
    }

    public void AddCirclesSignup(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,  string transactionHash, string circlesAddress,
        string? tokenAddress)
    {
        PrepareAddCirclesSignupInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesSignupInsertCmd!.Transaction = _transaction;
        _addCirclesSignupInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesSignupInsertCmd.Parameters["@timestamp"].Value = timestamp;
        _addCirclesSignupInsertCmd.Parameters["@transaction_index"].Value = transactionIndex;
        _addCirclesSignupInsertCmd.Parameters["@log_index"].Value = logIndex;
        _addCirclesSignupInsertCmd.Parameters["@transactionHash"].Value = transactionHash;
        _addCirclesSignupInsertCmd.Parameters["@circlesAddress"].Value = circlesAddress;
        _addCirclesSignupInsertCmd.Parameters["@tokenAddress"].Value =
            (object?)tokenAddress ?? DBNull.Value;
        _addCirclesSignupInsertCmd.ExecuteNonQuery();

        _transactionCounter++;
    }

    public void AddCirclesTrust(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,  string transactionHash, string userAddress,
        string canSendToAddress,
        int limit)
    {
        PrepareAddCirclesTrustInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesTrustInsertCmd!.Transaction = _transaction;
        _addCirclesTrustInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesTrustInsertCmd.Parameters["@timestamp"].Value = timestamp;
        _addCirclesTrustInsertCmd.Parameters["@transaction_index"].Value = transactionIndex;
        _addCirclesTrustInsertCmd.Parameters["@log_index"].Value = logIndex;
        _addCirclesTrustInsertCmd.Parameters["@transactionHash"].Value = transactionHash;
        _addCirclesTrustInsertCmd.Parameters["@userAddress"].Value = userAddress;
        _addCirclesTrustInsertCmd.Parameters["@canSendToAddress"].Value = canSendToAddress;
        _addCirclesTrustInsertCmd.Parameters["@limit"].Value = limit;
        _addCirclesTrustInsertCmd.ExecuteNonQuery();

        _transactionCounter++;
    }

    public void AddCirclesHubTransfer(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,  string transactionHash, string fromAddress,
        string toAddress, string amount)
    {
        PrepareAddCirclesHubTransferInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesHubTransferInsertCmd!.Transaction = _transaction;
        _addCirclesHubTransferInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesHubTransferInsertCmd.Parameters["@timestamp"].Value = timestamp;
        _addCirclesHubTransferInsertCmd.Parameters["@transaction_index"].Value = transactionIndex;
        _addCirclesHubTransferInsertCmd.Parameters["@log_index"].Value = logIndex;
        _addCirclesHubTransferInsertCmd.Parameters["@transactionHash"].Value = transactionHash;
        _addCirclesHubTransferInsertCmd.Parameters["@fromAddress"].Value = fromAddress;
        _addCirclesHubTransferInsertCmd.Parameters["@toAddress"].Value = toAddress;
        _addCirclesHubTransferInsertCmd.Parameters["@amount"].Value = amount;
        _addCirclesHubTransferInsertCmd.ExecuteNonQuery();

        _transactionCounter++;
    }

    public void AddCirclesTransfer(long blockNumber, ulong timestamp, int transactionIndex, int logIndex,  string transactionHash, string tokenAddress,
        string from, string to, UInt256 value)
    {
        PrepareAddCirclesTransferInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesTransferInsertCmd!.Transaction = _transaction;
        _addCirclesTransferInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesTransferInsertCmd.Parameters["@timestamp"].Value = timestamp;
        _addCirclesTransferInsertCmd.Parameters["@transaction_index"].Value = transactionIndex;
        _addCirclesTransferInsertCmd.Parameters["@log_index"].Value = logIndex;
        _addCirclesTransferInsertCmd.Parameters["@transactionHash"].Value = transactionHash;
        _addCirclesTransferInsertCmd.Parameters["@tokenAddress"].Value = tokenAddress;
        _addCirclesTransferInsertCmd.Parameters["@fromAddress"].Value = from;
        _addCirclesTransferInsertCmd.Parameters["@toAddress"].Value = to;
        _addCirclesTransferInsertCmd.Parameters["@amount"].Value = value.ToString(CultureInfo.InvariantCulture);
        _addCirclesTransferInsertCmd.ExecuteNonQuery();

        _transactionCounter++;
    }

    /// <summary>
    /// Commits any remaining transactions.
    /// </summary>
    public void Flush()
    {
        CommitTransaction();
    }

    public void Dispose()
    {
        CommitTransaction(); // Ensure any remaining transactions are committed before disposing.

        _addRelevantBlockInsertCmd?.Dispose();
        _addCirclesSignupInsertCmd?.Dispose();
        _addCirclesTrustInsertCmd?.Dispose();
        _addCirclesHubTransferInsertCmd?.Dispose();
        _addCirclesTransferInsertCmd?.Dispose();

        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        CommitTransaction(); // Ensure any remaining transactions are committed before disposing.

        _addRelevantBlockInsertCmd?.Dispose();
        _addIrrelevantBlockInsertCmd?.Dispose();
        _addCirclesSignupInsertCmd?.Dispose();
        _addCirclesTrustInsertCmd?.Dispose();
        _addCirclesHubTransferInsertCmd?.Dispose();
        _addCirclesTransferInsertCmd?.Dispose();

        await _connection.DisposeAsync();
    }

    private void PrepareAddRelevantBlockInsertCommand()
    {
        if (_addRelevantBlockInsertCmd != null) return;

        _addRelevantBlockInsertCmd = _connection.CreateCommand();
        _addRelevantBlockInsertCmd.CommandText = @$"
            INSERT INTO {TableNames.BlockRelevant} (block_number, timestamp, block_hash)
            VALUES (@blockNumber, @timestamp, @blockHash);
        ";

        _addRelevantBlockInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addRelevantBlockInsertCmd.Parameters.AddWithValue("@timestamp", 0);
        _addRelevantBlockInsertCmd.Parameters.AddWithValue("@blockHash", "");
    }

    private void PrepareAddIrrelevantBlockInsertCommand()
    {
        if (_addIrrelevantBlockInsertCmd != null) return;

        _addIrrelevantBlockInsertCmd = _connection.CreateCommand();
        _addIrrelevantBlockInsertCmd.CommandText = @$"
            INSERT INTO {TableNames.BlockIrrelevant} (block_number, timestamp, block_hash)
            VALUES (@blockNumber, @timestamp, @blockHash);
        ";

        _addIrrelevantBlockInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addIrrelevantBlockInsertCmd.Parameters.AddWithValue("@timestamp", 0);
        _addIrrelevantBlockInsertCmd.Parameters.AddWithValue("@blockHash", "");
    }

    private void PrepareAddCirclesSignupInsertCommand()
    {
        if (_addCirclesSignupInsertCmd != null) return;

        _addCirclesSignupInsertCmd = _connection.CreateCommand();
        _addCirclesSignupInsertCmd.CommandText = @$"
            INSERT INTO {TableNames.CirclesSignup} (block_number, timestamp, transaction_index, log_index, transaction_hash, circles_address, token_address)
            VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @circlesAddress, @tokenAddress);
        ";

        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@timestamp", 0);
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@transaction_index", 0);
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@log_index", 0);
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@transactionHash", "");
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@circlesAddress", "");
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@tokenAddress", "");
    }

    private void PrepareAddCirclesTrustInsertCommand()
    {
        if (_addCirclesTrustInsertCmd != null) return;

        _addCirclesTrustInsertCmd = _connection.CreateCommand();
        _addCirclesTrustInsertCmd.CommandText = @$"
            INSERT INTO {TableNames.CirclesTrust} (block_number, timestamp, transaction_index, log_index, transaction_hash, user_address, can_send_to_address, ""limit"")
            VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @userAddress, @canSendToAddress, @limit);
        ";

        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@timestamp", 0);
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@transaction_index", 0);
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@log_index", 0);
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@transactionHash", "");
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@userAddress", "");
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@canSendToAddress", "");
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@limit", 0);
    }

    private void PrepareAddCirclesHubTransferInsertCommand()
    {
        if (_addCirclesHubTransferInsertCmd != null) return;

        _addCirclesHubTransferInsertCmd = _connection.CreateCommand();
        _addCirclesHubTransferInsertCmd.CommandText = @$"
            INSERT INTO {TableNames.CirclesHubTransfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, from_address, to_address, amount)
            VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @fromAddress, @toAddress, @amount);
        ";

        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@timestamp", 0);
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@transaction_index", 0);
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@log_index", 0);
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@transactionHash", "");
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@fromAddress", "");
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@toAddress", "");
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@amount", "");
    }

    private void PrepareAddCirclesTransferInsertCommand()
    {
        if (_addCirclesTransferInsertCmd != null) return;

        _addCirclesTransferInsertCmd = _connection.CreateCommand();
        _addCirclesTransferInsertCmd.CommandText = @$"
            INSERT INTO {TableNames.CirclesTransfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, token_address, from_address, to_address, amount)
            VALUES (@blockNumber, @timestamp, @transaction_index, @log_index, @transactionHash, @tokenAddress, @fromAddress, @toAddress, @amount);
        ";

        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@timestamp", 0);
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@transaction_index", 0);
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@log_index", 0);
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@transactionHash", "");
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@tokenAddress", "");
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@fromAddress", "");
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@toAddress", "");
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@amount", "");
    }

    private void BeginTransaction()
    {
        if (_transaction != null) return;
        _transaction = _connection.BeginTransaction();
    }

    private void CommitTransaction()
    {
        _transaction?.Commit();
        _transaction = null;
        _transactionCounter = 0;
    }

    private void LogBlockThroughput()
    {
        if (_blocksProcessedCounter % 5000 != 0)
        {
            return;
        }

        double blocksPerSecond = _blocksProcessedCounter / _stopwatch.Elapsed.TotalSeconds;
        _logger?.Info(
            $"Processed {_blocksProcessedCounter} blocks in {_stopwatch.Elapsed.TotalSeconds} seconds. Current speed: {blocksPerSecond} blocks/sec.");
    }
}
