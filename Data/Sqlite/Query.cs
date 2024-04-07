using System.Data;
using System.Globalization;
using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
using Npgsql;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Circles.Index.Data.Postgresql;

public static class Query
{
    public static long? LatestBlock(NpgsqlConnection connection)
    {
        NpgsqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT MAX(block_number) as block_number FROM {TableNames.Block}
        ";

        object? result = cmd.ExecuteScalar();
        if (result is long longResult)
        {
            return longResult;
        }

        return null;
    }

    public static long? FirstGap(NpgsqlConnection connection)
    {
        NpgsqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT (prev.block_number + 1) AS gap_start
            FROM (SELECT block_number, LEAD(block_number) OVER (ORDER BY block_number) AS next_block_number FROM (SELECT block_number FROM {TableNames.Block} ORDER BY block.block_number DESC LIMIT 500000)) AS prev
            WHERE prev.next_block_number - prev.block_number > 1
            ORDER BY gap_start
            LIMIT 1;
        ";

        object? result = cmd.ExecuteScalar();
        if (result is long longResult)
        {
            return longResult;
        }

        return null;
    }

    /*

 */

    public static IEnumerable<(long BlockNumber, Hash256 BlockHash)> LastPersistedBlocks(NpgsqlConnection connection,
        int count = 100)
    {
        NpgsqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT block_number, decode(block_hash, 'hex')
            FROM {TableNames.Block}
            ORDER BY block_number DESC
            LIMIT {count}
        ";

        using NpgsqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return (reader.GetInt64(0), new Hash256((byte[])reader.GetValue(1)));
        }
    }

    public static IEnumerable<Address> TokenAddressesForAccount(NpgsqlConnection connection, Address circlesAccount)
    {
        const string sql = @$"
            select decode(token_address, 'hex')
            from {TableNames.Erc20Transfer}
            where decode(to_address, 'hex') = @circlesAccount
            group by token_address;";

        using NpgsqlCommand selectCmd = connection.CreateCommand();
        selectCmd.CommandText = sql;
        selectCmd.Parameters.AddWithValue("@circlesAccount", circlesAccount.Bytes);

        using NpgsqlDataReader reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new Address((byte[])reader.GetValue(0));
        }
    }

    public static IEnumerable<CirclesSignupDto> CirclesSignups(NpgsqlConnection connection, CirclesSignupQuery query,
        bool closeConnection = false)
    {
        NpgsqlCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";

        var (cursorConditionSql, cursorParameters) =
            CursorUtils.GenerateCursorConditionAndParameters(query.Cursor, query.SortOrder);

        cmd.CommandText = $@"
            SELECT block_number, transaction_index, log_index, timestamp, encode(transaction_hash, 'hex'), encode(circles_address, 'hex'), encode(token_address, 'hex')
            FROM {TableNames.CirclesSignup}
            WHERE {cursorConditionSql}
            AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
            AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
            AND (@TransactionHash IS NULL OR encode(transaction_hash, 'hex') = @TransactionHash)
            AND (@UserAddress IS NULL OR encode(circles_address, 'hex') = @UserAddress)
            AND (@TokenAddress IS NULL OR encode(token_address, 'hex') = @TokenAddress)
            ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
            LIMIT @PageSize
        ";

        cmd.Parameters.AddRange(cursorParameters);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@UserAddress", query.UserAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@TokenAddress", query.TokenAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using NpgsqlDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesSignupDto(
                Timestamp: reader.GetInt64(3).ToString(),
                BlockNumber: blockNumber.ToString(NumberFormatInfo.InvariantInfo),
                TransactionHash: reader.GetString(4),
                CirclesAddress: reader.GetString(5),
                TokenAddress: reader.IsDBNull(6) ? null : reader.GetString(6),
                Cursor: cursor);
        }
    }

    public static IEnumerable<CirclesTrustDto> CirclesTrusts(NpgsqlConnection connection, CirclesTrustQuery query,
        bool closeConnection = false)
    {
        NpgsqlCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        var (cursorConditionSql, cursorParameters) =
            CursorUtils.GenerateCursorConditionAndParameters(query.Cursor, query.SortOrder);

        string whereAndSql = $@"
            {cursorConditionSql}
            AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
            AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
            AND (@TransactionHash IS NULL OR encode(transaction_hash, 'hex') = @TransactionHash)
            AND (@UserAddress IS NULL OR encode(user_address, 'hex') = @UserAddress)
            AND (@CanSendToAddress IS NULL OR encode(can_send_to_address, 'hex') = @CanSendToAddress)";

        string whereOrSql = $@"
            {cursorConditionSql}
            AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
            AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
            AND (@TransactionHash IS NULL OR encode(transaction_hash, 'hex') = @TransactionHash)
            AND ((@UserAddress IS NULL OR encode(user_address, 'hex') = @UserAddress)
                  OR (@CanSendToAddress IS NULL OR encode(can_send_to_address, 'hex') = @CanSendToAddress))";

        cmd.CommandText = $@"
            SELECT block_number, transaction_index, log_index, timestamp, encode(transaction_hash, 'hex'), encode(user_address, 'hex'), encode(can_send_to_address, 'hex'), ""limit""
            FROM {TableNames.CirclesTrust}
                        WHERE {(query.Mode == QueryMode.And ? whereAndSql : whereOrSql)}
            ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
            LIMIT @PageSize
            ";

        cmd.Parameters.AddRange(cursorParameters);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@UserAddress", query.UserAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CanSendToAddress", query.CanSendToAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using NpgsqlDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesTrustDto(
                Timestamp: reader.GetInt64(3).ToString(),
                BlockNumber: blockNumber.ToString(NumberFormatInfo.InvariantInfo),
                TransactionHash: reader.GetString(4),
                UserAddress: reader.GetString(5),
                CanSendToAddress: reader.GetString(6),
                Limit: reader.GetInt32(7),
                Cursor: cursor);
        }
    }

    public static IEnumerable<CirclesHubTransferDto> CirclesHubTransfers(NpgsqlConnection connection,
        CirclesHubTransferQuery query, bool closeConnection = false)
    {
        NpgsqlCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        var (cursorConditionSql, cursorParameters) =
            CursorUtils.GenerateCursorConditionAndParameters(query.Cursor, query.SortOrder);

        string whereAndSql = $@"
        {cursorConditionSql}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR encode(transaction_hash, 'hex') = @TransactionHash)
        AND (@FromAddress IS NULL OR encode(from_address, 'hex') = @FromAddress)
        AND (@ToAddress IS NULL OR encode(to_address, 'hex') = @ToAddress)";

        string whereOrSql = $@"
        {cursorConditionSql}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR encode(transaction_hash, 'hex') = @TransactionHash)
        AND ((@FromAddress IS NULL OR encode(from_address, 'hex') = @FromAddress)
              OR (@ToAddress IS NULL OR encode(to_address, 'hex') = @ToAddress))";

        cmd.CommandText = $@"
        SELECT block_number, transaction_index, log_index, timestamp, encode(transaction_hash, 'hex'), encode(from_address, 'hex'), encode(to_address, 'hex'), amount
        FROM {TableNames.CirclesHubTransfer}
        WHERE {(query.Mode == QueryMode.And ? whereAndSql : whereOrSql)}
        ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
        LIMIT @PageSize
    ";

        cmd.Parameters.AddRange(cursorParameters);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@FromAddress", query.FromAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ToAddress", query.ToAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using NpgsqlDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesHubTransferDto(
                Timestamp: reader.GetInt64(3).ToString(),
                BlockNumber: blockNumber.ToString(NumberFormatInfo.InvariantInfo),
                TransactionHash: reader.GetString(4),
                FromAddress: reader.GetString(5),
                ToAddress: reader.GetString(6),
                Amount: reader.GetString(7),
                Cursor: cursor);
        }
    }

    public static IEnumerable<CirclesTransferDto> CirclesTransfers(NpgsqlConnection connection,
        CirclesTransferQuery query, bool closeConnection = false)
    {
        NpgsqlCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        var (cursorConditionSql, cursorParameters) =
            CursorUtils.GenerateCursorConditionAndParameters(query.Cursor, query.SortOrder);

        string whereAndSql = $@"
        {cursorConditionSql}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR encode(transaction_hash, 'hex') = @TransactionHash)
        AND (@TokenAddress IS NULL OR encode(token_address, 'hex') = @TokenAddress)
        AND (@FromAddress IS NULL OR encode(from_address, 'hex') = @FromAddress)
        AND (@ToAddress IS NULL OR encode(to_address, 'hex') = @ToAddress)";

        string whereOrSql = $@"
        {cursorConditionSql}
        AND (@MinBlockNumber = 0 OR block_number >= @MinBlockNumber)
        AND (@MaxBlockNumber = 0 OR block_number <= @MaxBlockNumber)
        AND (@TransactionHash IS NULL OR encode(transaction_hash, 'hex') = @TransactionHash)
        AND (@TokenAddress IS NULL OR encode(token_address, 'hex') = @TokenAddress)
        AND ((@FromAddress IS NULL OR encode(from_address, 'hex') = @FromAddress)
              OR (@ToAddress IS NULL OR encode(to_address, 'hex') = @ToAddress))";

        cmd.CommandText = $@"
        SELECT block_number, transaction_index, log_index, timestamp, encode(transaction_hash, 'hex'), encode(token_address, 'hex'), encode(from_address, 'hex'), encode(to_address, 'hex'), amount
        FROM {TableNames.Erc20Transfer}
        WHERE {(query.Mode == QueryMode.And ? whereAndSql : whereOrSql)}
        ORDER BY block_number {sortOrder}, transaction_index {sortOrder}, log_index {sortOrder}
        LIMIT @PageSize
    ";

        cmd.Parameters.AddRange(cursorParameters);
        cmd.Parameters.AddWithValue("@MinBlockNumber", query.BlockNumberRange.Min);
        cmd.Parameters.AddWithValue("@MaxBlockNumber", query.BlockNumberRange.Max);
        cmd.Parameters.AddWithValue("@TransactionHash", query.TransactionHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@TokenAddress", query.TokenAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@FromAddress", query.FromAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ToAddress", query.ToAddress?.ToLower() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PageSize", query.Limit ?? 100);

        using NpgsqlDataReader reader =
            cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            long blockNumber = reader.GetInt64(0);
            long transactionIndex = reader.GetInt64(1);
            long logIndex = reader.GetInt64(2);
            string cursor = $"{blockNumber}-{transactionIndex}-{logIndex}";

            yield return new CirclesTransferDto(
                Timestamp: reader.GetInt64(3).ToString(),
                BlockNumber: blockNumber.ToString(NumberFormatInfo.InvariantInfo),
                TransactionHash: reader.GetString(4),
                TokenAddress: reader.GetString(5),
                FromAddress: reader.GetString(6),
                ToAddress: reader.GetString(7),
                Amount: reader.GetString(8),
                Cursor: cursor);
        }
    }
}

// CursorUtils remains unchanged, it's generic enough to not require modification.