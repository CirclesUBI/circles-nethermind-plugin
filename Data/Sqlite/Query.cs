using System.Data;
using Circles.Index.Data.Model;
using Microsoft.Data.Sqlite;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Circles.Index.Data.Sqlite;

public static class Query
{
    public static long? LatestBlock(SqliteConnection connection)
    {
        SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT MAX(block_number)
            FROM (
                SELECT MAX(block_number) as block_number FROM {TableNames.BlockRelevant}
                UNION
                SELECT MAX(block_number) as block_number FROM {TableNames.BlockIrrelevant}
            ) as max_blocks
        ";

        object? result = cmd.ExecuteScalar();
        if (result is long longResult)
        {
            return longResult;
        }

        return null;
    }

    public static long? LatestRelevantBlock(SqliteConnection connection)
    {
        SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT MAX(block_number)
            FROM {TableNames.BlockRelevant}
        ";

        object? result = cmd.ExecuteScalar();
        if (result is long longResult)
        {
            return longResult;
        }

        return null;
    }

    public static IEnumerable<(long BlockNumber, Keccak BlockHash)> LastPersistedBlocks(SqliteConnection connection,
        int count = 100)
    {
        SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT block_number, block_hash
            FROM {TableNames.BlockRelevant}
            ORDER BY block_number DESC
            LIMIT {count}
        ";

        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return (reader.GetInt64(0), new Keccak(reader.GetString(1)));
        }
    }

    public static IEnumerable<Address> TokenAddressesForAccount(SqliteConnection connection, Address circlesAccount)
    {
        const string sql = @$"
            select token_address
            from {TableNames.CirclesTransfer}
            where to_address = @circlesAccount
            group by token_address;";

        using SqliteCommand selectCmd = connection.CreateCommand();
        selectCmd.CommandText = sql;
        selectCmd.Parameters.AddWithValue("@circlesAccount", circlesAccount.ToString(true, false));

        using SqliteDataReader reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            string tokenAddress = reader.GetString(0);
            yield return new Address(tokenAddress);
        }
    }

    public static IEnumerable<CirclesSignupDto> CirclesSignups(SqliteConnection connection, CirclesSignupQuery query,
        bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";

        string cursorCondition = "";
        if (query.Cursor != null)
        {
            var cursorParts = query.Cursor.Split('-');
            if (cursorParts.Length == 3)
            {
                long cursorBlockNumber = long.Parse(cursorParts[0]);
                long cursorTransactionIndex = long.Parse(cursorParts[1]);
                long cursorLogIndex = long.Parse(cursorParts[2]);

                cursorCondition = query.SortOrder == SortOrder.Ascending
                    ? "(block_number > @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index > @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index > @CursorLogIndex))))"
                    : "(block_number < @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index < @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index < @CursorLogIndex))))";

                cmd.Parameters.AddWithValue("@CursorBlockNumber", cursorBlockNumber);
                cmd.Parameters.AddWithValue("@CursorTransactionIndex", cursorTransactionIndex);
                cmd.Parameters.AddWithValue("@CursorLogIndex", cursorLogIndex);
            }
        }
        else
        {
            cursorCondition = "1 = 1";
        }

        cmd.CommandText = $@"
            SELECT block_number, transaction_index, log_index, transaction_hash, circles_address, token_address
            FROM {TableNames.CirclesSignup}
            WHERE {cursorCondition}
            AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
            AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
            AND (@TransactionHash IS NULL OR transaction_hash = @TransactionHash)
            AND (@UserAddress IS NULL OR circles_address = @UserAddress)
            AND (@TokenAddress IS NULL OR token_address = @TokenAddress)
            ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
            LIMIT @PageSize
        ";

        cmd.Parameters.AddWithValue("@Cursor", query.Cursor ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@UserAddress", query.UserAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@TokenAddress", query.TokenAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using SqliteDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesSignupDto(
                BlockNumber: blockNumber,
                TransactionHash: reader.GetString(3),
                CirclesAddress: reader.GetString(4),
                TokenAddress: reader.IsDBNull(5) ? null : reader.GetString(5),
                Cursor: cursor);
        }
    }

    public static IEnumerable<CirclesTrustDto> CirclesTrusts(SqliteConnection connection, CirclesTrustQuery query,
        bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        string cursorCondition = "";
        if (query.Cursor != null)
        {
            var cursorParts = query.Cursor.Split('-');
            if (cursorParts.Length == 3)
            {
                long cursorBlockNumber = long.Parse(cursorParts[0]);
                long cursorTransactionIndex = long.Parse(cursorParts[1]);
                long cursorLogIndex = long.Parse(cursorParts[2]);

                cursorCondition = query.SortOrder == SortOrder.Ascending
                    ? "(block_number > @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index > @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index > @CursorLogIndex))))"
                    : "(block_number < @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index < @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index < @CursorLogIndex))))";

                cmd.Parameters.AddWithValue("@CursorBlockNumber", cursorBlockNumber);
                cmd.Parameters.AddWithValue("@CursorTransactionIndex", cursorTransactionIndex);
                cmd.Parameters.AddWithValue("@CursorLogIndex", cursorLogIndex);
            }
        }
        else
        {
            cursorCondition = query.SortOrder == SortOrder.Ascending ? "1 = 1" : "1 = 1";
        }

        cmd.CommandText = $@"
        SELECT block_number, transaction_index, log_index, transaction_hash, user_address, can_send_to_address, ""limit""
        FROM {TableNames.CirclesTrust}
        WHERE {cursorCondition}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR transaction_hash = @TransactionHash)
        AND (@UserAddress IS NULL OR user_address = @UserAddress)
        AND (@CanSendToAddress IS NULL OR can_send_to_address = @CanSendToAddress)
        ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
        LIMIT @PageSize
    ";

        cmd.Parameters.AddWithValue("@Cursor", query.Cursor ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@UserAddress", query.UserAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CanSendToAddress", query.CanSendToAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using SqliteDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesTrustDto(
                BlockNumber: blockNumber,
                TransactionHash: reader.GetString(3),
                UserAddress: reader.GetString(4),
                CanSendToAddress: reader.GetString(5),
                Limit: reader.GetInt32(6),
                Cursor: cursor);
        }
    }

    public static IEnumerable<CirclesHubTransferDto> CirclesHubTransfers(SqliteConnection connection,
        CirclesHubTransferQuery query, bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        string cursorCondition = "";
        if (query.Cursor != null)
        {
            var cursorParts = query.Cursor.Split('-');
            if (cursorParts.Length == 3)
            {
                long cursorBlockNumber = long.Parse(cursorParts[0]);
                long cursorTransactionIndex = long.Parse(cursorParts[1]);
                long cursorLogIndex = long.Parse(cursorParts[2]);

                cursorCondition = query.SortOrder == SortOrder.Ascending
                    ? "(block_number > @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index > @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index > @CursorLogIndex))))"
                    : "(block_number < @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index < @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index < @CursorLogIndex))))";

                cmd.Parameters.AddWithValue("@CursorBlockNumber", cursorBlockNumber);
                cmd.Parameters.AddWithValue("@CursorTransactionIndex", cursorTransactionIndex);
                cmd.Parameters.AddWithValue("@CursorLogIndex", cursorLogIndex);
            }
        }
        else
        {
            cursorCondition = query.SortOrder == SortOrder.Ascending ? "1 = 1" : "1 = 1";
        }

        string whereAndSql = $@"
        {cursorCondition}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR transaction_hash = @TransactionHash)
        AND (@FromAddress IS NULL OR from_address = @FromAddress)
        AND (@ToAddress IS NULL OR to_address = @ToAddress)";

        string whereOrSql = $@"
        {cursorCondition}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR transaction_hash = @TransactionHash)
        AND ((@FromAddress IS NULL OR from_address = @FromAddress)
              OR (@ToAddress IS NULL OR to_address = @ToAddress))";

        cmd.CommandText = $@"
        SELECT block_number, transaction_index, log_index, transaction_hash, from_address, to_address, amount
        FROM {TableNames.CirclesHubTransfer}
        WHERE {(query.Mode == QueryMode.And ? whereAndSql : whereOrSql)}
        ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
        LIMIT @PageSize
    ";

        cmd.Parameters.AddWithValue("@Cursor", query.Cursor ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@FromAddress", query.FromAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ToAddress", query.ToAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using SqliteDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesHubTransferDto(
                BlockNumber: blockNumber,
                TransactionHash: reader.GetString(3),
                FromAddress: reader.GetString(4),
                ToAddress: reader.GetString(5),
                Amount: reader.GetString(6),
                Cursor: cursor);
        }
    }

    public static IEnumerable<CirclesTransferDto> CirclesTransfers(SqliteConnection connection,
        CirclesTransferQuery query, bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        string cursorCondition = "";
        if (query.Cursor != null)
        {
            var cursorParts = query.Cursor.Split('-');
            if (cursorParts.Length == 3)
            {
                long cursorBlockNumber = long.Parse(cursorParts[0]);
                long cursorTransactionIndex = long.Parse(cursorParts[1]);
                long cursorLogIndex = long.Parse(cursorParts[2]);

                cursorCondition = query.SortOrder == SortOrder.Ascending
                    ? "(block_number > @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index > @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index > @CursorLogIndex))))"
                    : "(block_number < @CursorBlockNumber OR (block_number = @CursorBlockNumber AND (transaction_index < @CursorTransactionIndex OR (transaction_index = @CursorTransactionIndex AND log_index < @CursorLogIndex))))";

                cmd.Parameters.AddWithValue("@CursorBlockNumber", cursorBlockNumber);
                cmd.Parameters.AddWithValue("@CursorTransactionIndex", cursorTransactionIndex);
                cmd.Parameters.AddWithValue("@CursorLogIndex", cursorLogIndex);
            }
        }
        else
        {
            cursorCondition = query.SortOrder == SortOrder.Ascending ? "1 = 1" : "1 = 1";
        }

        string whereAndSql = $@"
        {cursorCondition}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR transaction_hash = @TransactionHash)
        AND (@TokenAddress IS NULL OR token_address = @TokenAddress)
        AND (@FromAddress IS NULL OR from_address = @FromAddress)
        AND (@ToAddress IS NULL OR to_address = @ToAddress)";

        string whereOrSql = $@"
        {cursorCondition}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR transaction_hash = @TransactionHash)
        AND (@TokenAddress IS NULL OR token_address = @TokenAddress)
        AND ((@FromAddress IS NULL OR from_address = @FromAddress)
              OR (@ToAddress IS NULL OR to_address = @ToAddress))";

        cmd.CommandText = $@"
        SELECT block_number, transaction_index, log_index, transaction_hash, token_address, from_address, to_address, amount
        FROM {TableNames.CirclesTransfer}
        WHERE {(query.Mode == QueryMode.And ? whereAndSql : whereOrSql)}
        ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
        LIMIT @PageSize
    ";

        cmd.Parameters.AddWithValue("@Cursor", query.Cursor ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@TokenAddress", query.TokenAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@FromAddress", query.FromAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ToAddress", query.ToAddress ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using SqliteDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesTransferDto(
                BlockNumber: blockNumber,
                TransactionHash: reader.GetString(3),
                TokenAddress: reader.GetString(4),
                FromAddress: reader.GetString(5),
                ToAddress: reader.GetString(6),
                Amount: reader.GetString(7),
                Cursor: cursor);
        }
    }
}