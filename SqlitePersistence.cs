using System.Diagnostics;
using ConcurrentCollections;
using Microsoft.Data.Sqlite;
using Nethermind.Core;
using Nethermind.Logging;

namespace Circles.Index;

public class SqlitePersistence : IDisposable, IAsyncDisposable
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

    private readonly ConcurrentHashSet<Address> _circlesTokens = new();

    private readonly Stopwatch _stopwatch = new();
    private long _blocksProcessed;

    private readonly ILogger? _logger;

    public SqlitePersistence(string dbPath, int insertBatchSize, ILogger? logger)
    {
        if (insertBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(insertBatchSize), "Insert batch size must be greater than 0.");
        }

        _logger = logger;
        _logger?.Info("SQLite database at: " + dbPath);

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        _transactionLimit = insertBatchSize;
        _stopwatch.Start();
    }

    public void Initialize()
    {
        _logger?.Debug("Creating tables in SQLite db if they don't exist yet ..");

        using SqliteCommand createRelevantBlockTableCmd = _connection.CreateCommand();
        createRelevantBlockTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS block_relevant (
                block_number INTEGER PRIMARY KEY
            );
        ";
        createRelevantBlockTableCmd.ExecuteNonQuery();

        using SqliteCommand createIrrelevantBlockTableCmd = _connection.CreateCommand();
        createIrrelevantBlockTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS block_irrelevant (
                block_number INTEGER PRIMARY KEY
            );
        ";
        createIrrelevantBlockTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesSignupTableCmd = _connection.CreateCommand();
        createCirclesSignupTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS circles_signup (
                block_number INTEGER,
                transaction_hash TEXT,
                circles_address TEXT PRIMARY KEY,
                token_address TEXT NULL
            );
        ";
        createCirclesSignupTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesTrustTableCmd = _connection.CreateCommand();
        createCirclesTrustTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS circles_trust (
                block_number INTEGER,
                transaction_hash TEXT,
                user_address TEXT,
                can_send_to_address TEXT,
                ""limit"" INTEGER
            );
        ";
        createCirclesTrustTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesHubTransferTableCmd = _connection.CreateCommand();
        createCirclesHubTransferTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS circles_hub_transfer (
                block_number INTEGER,
                transaction_hash TEXT,
                from_address TEXT,
                to_address TEXT,
                amount INTEGER
            );
        ";
        createCirclesHubTransferTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesTransferTableCmd = _connection.CreateCommand();
        createCirclesTransferTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS circles_transfer (
                block_number INTEGER,
                transaction_hash TEXT,
                token_address TEXT,
                from_address TEXT,
                to_address TEXT,
                amount INTEGER
            );
        ";
        createCirclesTransferTableCmd.ExecuteNonQuery();

        _logger?.Info("SQLite db schema initialized.");

        using SqliteCommand createIndexesCmd = _connection.CreateCommand();
        createIndexesCmd.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_circles_trust_user_address ON circles_trust (user_address);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_can_send_to_address ON circles_trust (can_send_to_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_from_address ON circles_hub_transfer (from_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_to_address ON circles_hub_transfer (to_address);
            CREATE INDEX IF NOT EXISTS idx_circles_transfer_from_address ON circles_transfer (from_address);
            CREATE INDEX IF NOT EXISTS idx_circles_transfer_to_address ON circles_transfer (to_address);
        ";
        createIndexesCmd.ExecuteNonQuery();

        _logger?.Debug("Warming up in-memory token cache ..");

        // Warm up the caches (read all circles tokens)
        using SqliteCommand selectCirclesTokensCmd = _connection.CreateCommand();
        selectCirclesTokensCmd.CommandText = @"
            SELECT token_address
            FROM circles_signup
            WHERE token_address IS NOT NULL;
        ";
        using SqliteDataReader circlesTokenReader = selectCirclesTokensCmd.ExecuteReader();
        while (circlesTokenReader.Read())
        {
            string tokenAddress = circlesTokenReader.GetString(0);
            _circlesTokens.Add(new Address(tokenAddress));
        }

        circlesTokenReader.Close();

        _logger?.Info($"Loaded {_circlesTokens.Count} CRC token addresses into cache.");
    }

    public bool IsCirclesToken(Address address)
    {
        return _circlesTokens.Contains(address);
    }

    public void AddRelevantBlock(long blockNumber)
    {
        PrepareAddRelevantBlockInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addRelevantBlockInsertCmd!.Transaction = _transaction;
        _addRelevantBlockInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addRelevantBlockInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            CommitTransaction();
        }

        _blocksProcessed++;
        LogBlockThroughput();
    }

    public void AddIrrelevantBlock(long blockNumber)
    {
        PrepareAddIrrelevantBlockInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addIrrelevantBlockInsertCmd!.Transaction = _transaction;
        _addIrrelevantBlockInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addIrrelevantBlockInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            CommitTransaction();
        }

        _blocksProcessed++;
        LogBlockThroughput();
    }

    public void AddCirclesSignup(long blockNumber, string transactionHash, string circlesAddress, string? tokenAddress)
    {
        PrepareAddCirclesSignupInsertCommand();

        if (tokenAddress != null)
        {
            _circlesTokens.Add(new Address(tokenAddress));
        }

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesSignupInsertCmd!.Transaction = _transaction;
        _addCirclesSignupInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesSignupInsertCmd.Parameters["@transactionHash"].Value = transactionHash;
        _addCirclesSignupInsertCmd.Parameters["@circlesAddress"].Value = circlesAddress;
        _addCirclesSignupInsertCmd.Parameters["@tokenAddress"].Value = (object?)tokenAddress ?? DBNull.Value;
        _addCirclesSignupInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            CommitTransaction();
        }
    }

    public void AddCirclesTrust(long blockNumber, string toString, string userAddress, string canSendToAddress,
        int limit)
    {
        PrepareAddCirclesTrustInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesTrustInsertCmd!.Transaction = _transaction;
        _addCirclesTrustInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesTrustInsertCmd.Parameters["@transactionHash"].Value = toString;
        _addCirclesTrustInsertCmd.Parameters["@userAddress"].Value = userAddress;
        _addCirclesTrustInsertCmd.Parameters["@canSendToAddress"].Value = canSendToAddress;
        _addCirclesTrustInsertCmd.Parameters["@limit"].Value = limit;
        _addCirclesTrustInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            CommitTransaction();
        }
    }

    public void AddCirclesHubTransfer(long blockNumber, string toString, string fromAddress, string toAddress,
        string amount)
    {
        PrepareAddCirclesHubTransferInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesHubTransferInsertCmd!.Transaction = _transaction;
        _addCirclesHubTransferInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesHubTransferInsertCmd.Parameters["@transactionHash"].Value = toString;
        _addCirclesHubTransferInsertCmd.Parameters["@fromAddress"].Value = fromAddress;
        _addCirclesHubTransferInsertCmd.Parameters["@toAddress"].Value = toAddress;
        _addCirclesHubTransferInsertCmd.Parameters["@amount"].Value = amount;
        _addCirclesHubTransferInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            CommitTransaction();
        }
    }

    public void AddCirclesTransfer(long blockNumber, string toString, string tokenAddress, string from, string to,
        string value)
    {
        PrepareAddCirclesTransferInsertCommand();

        if (_transactionCounter == 0)
        {
            BeginTransaction();
        }

        _addCirclesTransferInsertCmd!.Transaction = _transaction;
        _addCirclesTransferInsertCmd.Parameters["@blockNumber"].Value = blockNumber;
        _addCirclesTransferInsertCmd.Parameters["@transactionHash"].Value = toString;
        _addCirclesTransferInsertCmd.Parameters["@tokenAddress"].Value = tokenAddress;
        _addCirclesTransferInsertCmd.Parameters["@fromAddress"].Value = from;
        _addCirclesTransferInsertCmd.Parameters["@toAddress"].Value = to;
        _addCirclesTransferInsertCmd.Parameters["@amount"].Value = value;
        _addCirclesTransferInsertCmd.ExecuteNonQuery();

        _transactionCounter++;

        if (_transactionCounter >= _transactionLimit)
        {
            CommitTransaction();
        }
    }

    public Address[] GetTokenAddressesForAccount(Address circlesAccount)
    {
        const string sql = @"
            select token_address
            from circles_transfer
            where to_address = @circlesAccount
            group by token_address;";

        using SqliteCommand selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = sql;
        selectCmd.Parameters.AddWithValue("@circlesAccount", circlesAccount.ToString(true, false));

        using SqliteDataReader reader = selectCmd.ExecuteReader();
        List<Address> addresses = new();
        while (reader.Read())
        {
            string tokenAddress = reader.GetString(0);
            addresses.Add(new Address(tokenAddress));
        }

        return addresses.ToArray();
    }

    public long GetLastPersistedBlock()
    {
        SqliteCommand selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = @"
            with a as (
                select max(block_number) as block_number from block_relevant
                union all
                select max(block_number) as block_number from block_irrelevant
            )
            select max(block_number) as block_number from a;
        ";

        object? result = selectCmd.ExecuteScalar();
        if (result is null or DBNull)
        {
            return 0;
        }

        return (long)result;
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
        _addRelevantBlockInsertCmd.CommandText = @"
            INSERT INTO block_relevant (block_number)
            VALUES (@blockNumber);
        ";

        _addRelevantBlockInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
    }

    private void PrepareAddIrrelevantBlockInsertCommand()
    {
        if (_addIrrelevantBlockInsertCmd != null) return;

        _addIrrelevantBlockInsertCmd = _connection.CreateCommand();
        _addIrrelevantBlockInsertCmd.CommandText = @"
            INSERT INTO block_irrelevant (block_number)
            VALUES (@blockNumber);
        ";

        _addIrrelevantBlockInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
    }

    private void PrepareAddCirclesSignupInsertCommand()
    {
        if (_addCirclesSignupInsertCmd != null) return;

        _addCirclesSignupInsertCmd = _connection.CreateCommand();
        _addCirclesSignupInsertCmd.CommandText = @"
            INSERT INTO circles_signup (block_number, transaction_hash, circles_address, token_address)
            VALUES (@blockNumber, @transactionHash, @circlesAddress, @tokenAddress);
        ";

        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@transactionHash", "");
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@circlesAddress", "");
        _addCirclesSignupInsertCmd.Parameters.AddWithValue("@tokenAddress", "");
    }

    private void PrepareAddCirclesTrustInsertCommand()
    {
        if (_addCirclesTrustInsertCmd != null) return;

        _addCirclesTrustInsertCmd = _connection.CreateCommand();
        _addCirclesTrustInsertCmd.CommandText = @"
            INSERT INTO circles_trust (block_number, transaction_hash, user_address, can_send_to_address, ""limit"")
            VALUES (@blockNumber, @transactionHash, @userAddress, @canSendToAddress, @limit);
        ";

        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@transactionHash", "");
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@userAddress", "");
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@canSendToAddress", "");
        _addCirclesTrustInsertCmd.Parameters.AddWithValue("@limit", 0);
    }

    private void PrepareAddCirclesHubTransferInsertCommand()
    {
        if (_addCirclesHubTransferInsertCmd != null) return;

        _addCirclesHubTransferInsertCmd = _connection.CreateCommand();
        _addCirclesHubTransferInsertCmd.CommandText = @"
            INSERT INTO circles_hub_transfer (block_number, transaction_hash, from_address, to_address, amount)
            VALUES (@blockNumber, @transactionHash, @fromAddress, @toAddress, @amount);
        ";

        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@transactionHash", "");
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@fromAddress", "");
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@toAddress", "");
        _addCirclesHubTransferInsertCmd.Parameters.AddWithValue("@amount", "");
    }

    private void PrepareAddCirclesTransferInsertCommand()
    {
        if (_addCirclesTransferInsertCmd != null) return;

        _addCirclesTransferInsertCmd = _connection.CreateCommand();
        _addCirclesTransferInsertCmd.CommandText = @"
            INSERT INTO circles_transfer (block_number, transaction_hash, token_address, from_address, to_address, amount)
            VALUES (@blockNumber, @transactionHash, @tokenAddress, @fromAddress, @toAddress, @amount);
        ";
        _addCirclesTransferInsertCmd.Parameters.AddWithValue("@blockNumber", 0);
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
        if (_blocksProcessed % 5000 == 0)
        {
            double blocksPerSecond = _blocksProcessed / _stopwatch.Elapsed.TotalSeconds;
            _logger?.Info(
                $"Processed {_blocksProcessed} blocks in {_stopwatch.Elapsed.TotalSeconds} seconds. Current speed: {blocksPerSecond} blocks/sec.");
        }
    }

    public void Flush()
    {
        CommitTransaction();
    }

    public void DeleteFrom(long reorgAt)
    {
        _logger?.Info($"Deleting all data from block {reorgAt} onwards ..");

        using SqliteCommand deleteRelevantBlocksCmd = _connection.CreateCommand();
        deleteRelevantBlocksCmd.CommandText = @"
            DELETE FROM block_relevant
            WHERE block_number >= @reorgAt;
        ";
        deleteRelevantBlocksCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
        deleteRelevantBlocksCmd.ExecuteNonQuery();

        using SqliteCommand deleteIrrelevantBlocksCmd = _connection.CreateCommand();
        deleteIrrelevantBlocksCmd.CommandText = @"
            DELETE FROM block_irrelevant
            WHERE block_number >= @reorgAt;
        ";
        deleteIrrelevantBlocksCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
        deleteIrrelevantBlocksCmd.ExecuteNonQuery();

        using SqliteCommand deleteCirclesSignupCmd = _connection.CreateCommand();
        deleteCirclesSignupCmd.CommandText = @"
            DELETE FROM circles_signup
            WHERE block_number >= @reorgAt;
        ";
        deleteCirclesSignupCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
        deleteCirclesSignupCmd.ExecuteNonQuery();

        using SqliteCommand deleteCirclesTrustCmd = _connection.CreateCommand();
        deleteCirclesTrustCmd.CommandText = @"
            DELETE FROM circles_trust
            WHERE block_number >= @reorgAt;
        ";
        deleteCirclesTrustCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
        deleteCirclesTrustCmd.ExecuteNonQuery();

        using SqliteCommand deleteCirclesHubTransferCmd = _connection.CreateCommand();
        deleteCirclesHubTransferCmd.CommandText = @"
            DELETE FROM circles_hub_transfer
            WHERE block_number >= @reorgAt;
        ";
        deleteCirclesHubTransferCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
        deleteCirclesHubTransferCmd.ExecuteNonQuery();

        using SqliteCommand deleteCirclesTransferCmd = _connection.CreateCommand();
        deleteCirclesTransferCmd.CommandText = @"
            DELETE FROM circles_transfer
            WHERE block_number >= @reorgAt;
        ";
        deleteCirclesTransferCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
        deleteCirclesTransferCmd.ExecuteNonQuery();

        _logger?.Info($"Deleted all data from block {reorgAt} onwards.");
    }
}
