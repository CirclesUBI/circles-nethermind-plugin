using System.Collections;
using Circles.Index.Data.Model;
using Circles.Index.Data.Postgresql;
using Circles.Index.Data.Sqlite;
using Nethermind.Logging;
using Npgsql;

namespace Circles.Index.Indexer;

public static class ReorgHandler
{
    private record ReorgAffectedData(
        CirclesSignupDto[] Signups,
        CirclesTrustDto[] Trusts,
        CirclesHubTransferDto[] HubTransfers,
        CirclesTransferDto[] Transfers);

    public static async Task ReorgAt(NpgsqlConnection connection, ILogger logger, long block)
    {
        ReorgAffectedData affectedData = await GetAffectedItems(connection, block);
        logger.Info($"Deleting all blocks greater or equal {block} from the index ..");
        logger.Info($"Affected signups: {affectedData.Signups.Length}");
        logger.Info($"Affected trusts: {affectedData.Trusts.Length}");
        logger.Info($"Affected hub transfers: {affectedData.HubTransfers.Length}");
        logger.Info($"Affected crc transfers: {affectedData.Transfers.Length}");

        DeleteFromBlockOnwards(connection, block);
    }

    /// <summary>
    /// Deletes all data from the specified block number onwards.
    /// </summary>
    /// <param name="connection">The connection to the database</param>
    /// <param name="reorgAt">The block number to delete from (inclusive)</param>
    public static void DeleteFromBlockOnwards(NpgsqlConnection connection, long reorgAt)
    {
        using NpgsqlTransaction transaction = connection.BeginTransaction();
        try
        {
            using NpgsqlCommand deleteBlocksCmd = connection.CreateCommand();
            deleteBlocksCmd.CommandText = @$"
                DELETE FROM {TableNames.Block}
                WHERE block_number >= @reorgAt;
            ";
            deleteBlocksCmd.Transaction = transaction;
            deleteBlocksCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteBlocksCmd.ExecuteNonQuery();

            using NpgsqlCommand deleteCirclesSignupCmd = connection.CreateCommand();
            deleteCirclesSignupCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesSignup}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesSignupCmd.Transaction = transaction;
            deleteCirclesSignupCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesSignupCmd.ExecuteNonQuery();

            using NpgsqlCommand deleteCirclesTrustCmd = connection.CreateCommand();
            deleteCirclesTrustCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesTrust}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesTrustCmd.Transaction = transaction;
            deleteCirclesTrustCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesTrustCmd.ExecuteNonQuery();

            using NpgsqlCommand deleteCirclesHubTransferCmd = connection.CreateCommand();
            deleteCirclesHubTransferCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesHubTransfer}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesHubTransferCmd.Transaction = transaction;
            deleteCirclesHubTransferCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesHubTransferCmd.ExecuteNonQuery();

            using NpgsqlCommand deleteCirclesTransferCmd = connection.CreateCommand();
            deleteCirclesTransferCmd.CommandText = @$"
                DELETE FROM {TableNames.Erc20Transfer}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesTransferCmd.Transaction = transaction;
            deleteCirclesTransferCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesTransferCmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<ReorgAffectedData> GetAffectedItems(NpgsqlConnection connection, long reorgAt)
    {
        CirclesSignupQuery affectedSignupQuery = new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        CirclesSignupDto[] affectedSignups = Query.CirclesSignups(connection, affectedSignupQuery).ToArray();

        CirclesTrustQuery affectedTrustQuery = new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        CirclesTrustDto[] affectedTrusts = Query.CirclesTrusts(connection, affectedTrustQuery).ToArray();

        CirclesHubTransferQuery affectedHubTransferQuery =
            new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        CirclesHubTransferDto[] affectedHubTransfers =
             Query.CirclesHubTransfers(connection, affectedHubTransferQuery).ToArray();

        CirclesTransferQuery affectedTransferQuery =
            new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        CirclesTransferDto[] affectedTransfers =
            Query.CirclesTransfers(connection, affectedTransferQuery).ToArray();

        return new ReorgAffectedData(
            Signups: affectedSignups,
            Trusts: affectedTrusts,
            HubTransfers: affectedHubTransfers,
            Transfers: affectedTransfers);
    }
}