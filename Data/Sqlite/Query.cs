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

    public static IEnumerable<(long BlockNumber, Keccak BlockHash)> LastPersistedBlocks(SqliteConnection connection, int count = 16)
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
        int pageSize, bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        string cursorCondition = query.SortOrder == SortOrder.Ascending
            ? "(@Cursor IS NULL OR block_number > @Cursor)"
            : "(@Cursor IS NULL OR block_number < @Cursor)";

        cmd.CommandText = $@"
            SELECT block_number, transaction_hash, circles_address, token_address
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
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using SqliteDataReader reader = cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            yield return new CirclesSignupDto(
                BlockNumber: reader.GetInt64(0),
                TransactionHash: reader.GetString(1),
                CirclesAddress: reader.GetString(2),
                TokenAddress: reader.IsDBNull(3) ? null : reader.GetString(3));
        }
    }

    public static IEnumerable<CirclesTrustDto> CirclesTrusts(SqliteConnection connection, CirclesTrustQuery query,
        int pageSize, bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        string cursorCondition = query.SortOrder == SortOrder.Ascending
            ? "(@Cursor IS NULL OR block_number > @Cursor)"
            : "(@Cursor IS NULL OR block_number < @Cursor)";

        cmd.CommandText = $@"
            SELECT block_number, transaction_hash, user_address, can_send_to_address, ""limit""
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
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using SqliteDataReader reader = cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            yield return new CirclesTrustDto(
                BlockNumber: reader.GetInt64(0),
                TransactionHash: reader.GetString(1),
                UserAddress: reader.GetString(2),
                CanSendToAddress: reader.GetString(3),
                Limit: reader.GetInt32(4));
        }
    }

    public static IEnumerable<CirclesHubTransferDto> CirclesHubTransfers(SqliteConnection connection,
        CirclesHubTransferQuery query, int pageSize, bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        string cursorCondition = query.SortOrder == SortOrder.Ascending
            ? "(@Cursor IS NULL OR block_number > @Cursor)"
            : "(@Cursor IS NULL OR block_number < @Cursor)";

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
            SELECT block_number, transaction_hash, from_address, to_address, amount
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
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using SqliteDataReader reader = cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            yield return new CirclesHubTransferDto(
                BlockNumber: reader.GetInt64(0),
                TransactionHash: reader.GetString(1),
                FromAddress: reader.GetString(2),
                ToAddress: reader.GetString(3),
                Amount: reader.GetString(4));
        }
    }

    public static IEnumerable<CirclesTransferDto> CirclesTransfers(SqliteConnection connection, CirclesTransferQuery query,
        int pageSize, bool closeConnection = false)
    {
        SqliteCommand cmd = connection.CreateCommand();

        string sortOrder = query.SortOrder == SortOrder.Ascending ? "ASC" : "DESC";
        string cursorCondition = query.SortOrder == SortOrder.Ascending
            ? "(@Cursor IS NULL OR block_number > @Cursor)"
            : "(@Cursor IS NULL OR block_number < @Cursor)";

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
            SELECT block_number, transaction_hash, token_address, from_address, to_address, amount
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
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using SqliteDataReader reader = cmd.ExecuteReader(closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        while (reader.Read())
        {
            yield return new CirclesTransferDto(
                BlockNumber: reader.GetInt64(0),
                TransactionHash: reader.GetString(1),
                TokenAddress: reader.GetString(2),
                FromAddress: reader.GetString(3),
                ToAddress: reader.GetString(4),
                Amount: reader.GetString(5));
        }
    }
}
