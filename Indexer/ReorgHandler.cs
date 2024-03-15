using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Nethermind.Logging;

namespace Circles.Index.Indexer;

public static class ReorgHandler
{
    private record ReorgAffectedData(
        CirclesSignupDto[] Signups,
        CirclesTrustDto[] Trusts,
        CirclesHubTransferDto[] HubTransfers,
        CirclesTransferDto[] Transfers);

    public static async Task ReorgAt(SqliteConnection connection, ILogger logger, long block)
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
    public static void DeleteFromBlockOnwards(SqliteConnection connection, long reorgAt)
    {
        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            using SqliteCommand deleteRelevantBlocksCmd = connection.CreateCommand();
            deleteRelevantBlocksCmd.CommandText = @$"
                DELETE FROM {TableNames.BlockRelevant}
                WHERE block_number >= @reorgAt;
            ";
            deleteRelevantBlocksCmd.Transaction = transaction;
            deleteRelevantBlocksCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteRelevantBlocksCmd.ExecuteNonQuery();

            using SqliteCommand deleteIrrelevantBlocksCmd = connection.CreateCommand();
            deleteIrrelevantBlocksCmd.CommandText = @$"
                DELETE FROM {TableNames.BlockIrrelevant}
                WHERE block_number >= @reorgAt;
            ";
            deleteIrrelevantBlocksCmd.Transaction = transaction;
            deleteIrrelevantBlocksCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteIrrelevantBlocksCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesSignupCmd = connection.CreateCommand();
            deleteCirclesSignupCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesSignup}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesSignupCmd.Transaction = transaction;
            deleteCirclesSignupCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesSignupCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesTrustCmd = connection.CreateCommand();
            deleteCirclesTrustCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesTrust}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesTrustCmd.Transaction = transaction;
            deleteCirclesTrustCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesTrustCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesHubTransferCmd = connection.CreateCommand();
            deleteCirclesHubTransferCmd.CommandText = @$"
                DELETE FROM {TableNames.CirclesHubTransfer}
                WHERE block_number >= @reorgAt;
            ";
            deleteCirclesHubTransferCmd.Transaction = transaction;
            deleteCirclesHubTransferCmd.Parameters.AddWithValue("@reorgAt", reorgAt);
            deleteCirclesHubTransferCmd.ExecuteNonQuery();

            using SqliteCommand deleteCirclesTransferCmd = connection.CreateCommand();
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

    private static async Task<ReorgAffectedData> GetAffectedItems(SqliteConnection connection, long reorgAt)
    {
        CirclesSignupQuery affectedSignupQuery = new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        Task<CirclesSignupDto[]> affectedSignups =
            Task.Run(() => Query.CirclesSignups(connection, affectedSignupQuery).ToArray());

        CirclesTrustQuery affectedTrustQuery = new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        Task<CirclesTrustDto[]> affectedTrusts =
            Task.Run(() => Query.CirclesTrusts(connection, affectedTrustQuery).ToArray());

        CirclesHubTransferQuery affectedHubTransferQuery =
            new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        Task<CirclesHubTransferDto[]> affectedHubTransfers =
            Task.Run(() => Query.CirclesHubTransfers(connection, affectedHubTransferQuery).ToArray());

        CirclesTransferQuery affectedTransferQuery =
            new() { BlockNumberRange = { Min = reorgAt }, Limit = int.MaxValue };
        Task<CirclesTransferDto[]> affectedTransfers =
            Task.Run(() => Query.CirclesTransfers(connection, affectedTransferQuery).ToArray());

        await Task.WhenAll(
            affectedSignups,
            affectedTrusts,
            affectedHubTransfers,
            affectedTransfers
        );

        return new ReorgAffectedData(
            Signups: affectedSignups.Result,
            Trusts: affectedTrusts.Result,
            HubTransfers: affectedHubTransfers.Result,
            Transfers: affectedTransfers.Result);
    }
}