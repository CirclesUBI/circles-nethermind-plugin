using Npgsql;

namespace Circles.Index.Data.Postgresql;

public static class Schema
{
    public static void MigrateIndexes(NpgsqlConnection connection)
    {
        using NpgsqlCommand createIndexesCmd = connection.CreateCommand();
        createIndexesCmd.CommandText = @$"
            -- index on the transaction_hash column of all tables
            CREATE INDEX IF NOT EXISTS idx_circles_signup_transaction_hash ON {TableNames.CrcV1Signup} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_transaction_hash ON {TableNames.CrcV1Trust} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_transaction_hash ON {TableNames.CrcV1HubTransfer} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_erc20_transfer_transaction_hash ON {TableNames.Erc20Transfer} (transaction_hash);

            -- index on the block_number column of all tables
            CREATE UNIQUE INDEX IF NOT EXISTS idx_block_block_number ON {TableNames.Block} (block_number);

            -- index on the 'timstamp' column of all tables
            CREATE INDEX IF NOT EXISTS idx_block_timestamp ON {TableNames.Block} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_signup_timestamp ON {TableNames.CrcV1Signup} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_timestamp ON {TableNames.CrcV1Trust} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_timestamp ON {TableNames.CrcV1HubTransfer} (timestamp); 
            CREATE INDEX IF NOT EXISTS idx_erc20_transfer_timestamp ON {TableNames.Erc20Transfer} (timestamp);

            -- event specific indexes
            CREATE UNIQUE INDEX IF NOT EXISTS idx_circles_signup_user_address ON {TableNames.CrcV1Signup} (circles_address);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_circles_signup_token_address ON {TableNames.CrcV1Signup} (token_address);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_user_address ON {TableNames.CrcV1Trust} (user_address);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_can_send_to_address ON {TableNames.CrcV1Trust} (can_send_to_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_from_address ON {TableNames.CrcV1HubTransfer} (from_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_to_address ON {TableNames.CrcV1HubTransfer} (to_address);
            CREATE INDEX IF NOT EXISTS idx_erc20_transfer_from_address ON {TableNames.Erc20Transfer} (from_address);
            CREATE INDEX IF NOT EXISTS idx_erc20_transfer_to_address ON {TableNames.Erc20Transfer} (to_address);
        ";
        createIndexesCmd.ExecuteNonQuery();
    }

    public static void MigrateTables(NpgsqlConnection connection)
    {
        using NpgsqlCommand createBlockTableCmd = connection.CreateCommand();
        createBlockTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.Block} (
                block_number BIGINT PRIMARY KEY,
                timestamp BIGINT,
                block_hash TEXT
            );
        ";
        createBlockTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCirclesSignupTableCmd = connection.CreateCommand();
        createCirclesSignupTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV1Signup} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                circles_address TEXT,
                token_address TEXT NULL,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCirclesSignupTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCirclesTrustTableCmd = connection.CreateCommand();
        createCirclesTrustTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV1Trust} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                user_address TEXT,
                can_send_to_address TEXT,
                ""limit"" BIGINT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCirclesTrustTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCirclesHubTransferTableCmd = connection.CreateCommand();
        createCirclesHubTransferTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV1HubTransfer} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                from_address TEXT,
                to_address TEXT,
                amount NUMERIC,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCirclesHubTransferTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createErc20TransferTableCmd = connection.CreateCommand();
        createErc20TransferTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.Erc20Transfer} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                token_address TEXT,
                from_address TEXT,
                to_address TEXT,
                amount NUMERIC,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createErc20TransferTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCrcV2RegisterHumanTableCmd = connection.CreateCommand();
        createCrcV2RegisterHumanTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2RegisterHuman} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                address TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCrcV2RegisterHumanTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCrcV2InviteHumanTableCmd = connection.CreateCommand();
        createCrcV2InviteHumanTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2InviteHuman} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                inviter_address TEXT,
                invitee_address TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCrcV2InviteHumanTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCrcV2RegisterOrganizationTableCmd = connection.CreateCommand();
        createCrcV2RegisterOrganizationTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2RegisterOrganization} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                organization_address TEXT,
                organization_name TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCrcV2RegisterOrganizationTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCrcV2RegisterGroupTableCmd = connection.CreateCommand();
        createCrcV2RegisterGroupTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2RegisterGroup} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                group_address TEXT,
                group_mint_policy TEXT,
                group_treasury TEXT,
                group_name TEXT,
                group_symbol TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCrcV2RegisterGroupTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCrcV2PersonalMintTableCmd = connection.CreateCommand();
        createCrcV2PersonalMintTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2PersonalMint} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                to_address TEXT, 
                amount NUMERIC/*uint256*/, 
                start_period NUMERIC/*uint256*/, 
                end_period NUMERIC /*uint256*/,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCrcV2PersonalMintTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCrcV2ConvertInflationTableCmd = connection.CreateCommand();
        createCrcV2ConvertInflationTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2ConvertInflation} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                inflation_value NUMERIC /*uint256*/, 
                demurrage_value NUMERIC, 
                ""day"" NUMERIC /*uint64*/,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCrcV2ConvertInflationTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createCrcV2TrustTableCmd = connection.CreateCommand();
        createCrcV2TrustTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2Trust} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                truster_address TEXT,
                trustee_address TEXT,
                expiry_time NUMERIC,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";  
        createCrcV2TrustTableCmd.ExecuteNonQuery();
        
        using NpgsqlCommand createCrcV2StoppedTableCmd = connection.CreateCommand();
        createCrcV2StoppedTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CrcV2Stopped} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                address TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCrcV2StoppedTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createErc1155TransferSingleTableCmd = connection.CreateCommand();
        createErc1155TransferSingleTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.Erc1155TransferSingle} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                operator_address TEXT,
                from_address TEXT,
                to_address TEXT,
                token_id NUMERIC,
                amount NUMERIC,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createErc1155TransferSingleTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createErc1155TransferBatchTableCmd = connection.CreateCommand();
        createErc1155TransferBatchTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.Erc1155TransferBatch} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                operator_address TEXT,
                from_address TEXT,
                to_address TEXT,
                token_ids NUMERIC[],
                amounts NUMERIC[],
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createErc1155TransferBatchTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createErc1155ApprovalForAllTableCmd = connection.CreateCommand();
        createErc1155ApprovalForAllTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.Erc1155ApprovalForAll} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                owner_address TEXT,
                operator_address TEXT,
                approved BOOLEAN,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createErc1155ApprovalForAllTableCmd.ExecuteNonQuery();

        using NpgsqlCommand createErc1155UriTableCmd = connection.CreateCommand();
        createErc1155UriTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.Erc1155Uri} (
                block_number BIGINT,
                timestamp BIGINT,
                transaction_index INT,
                log_index INT,
                transaction_hash TEXT,
                token_id NUMERIC,
                uri TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createErc1155UriTableCmd.ExecuteNonQuery();
    }
}