using Circles.Index.Data.Sqlite;
using Npgsql;

namespace Circles.Index.Data.Postgresql;

public static class Schema
{
    public static void MigrateIndexes(NpgsqlConnection connection)
    {
        using NpgsqlCommand createIndexesCmd = connection.CreateCommand();
        createIndexesCmd.CommandText = @$"
            -- index on the transaction_hash column of all tables
            CREATE INDEX IF NOT EXISTS idx_circles_signup_transaction_hash ON {TableNames.CirclesSignup} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_transaction_hash ON {TableNames.CirclesTrust} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_transaction_hash ON {TableNames.CirclesHubTransfer} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_erc20_transfer_transaction_hash ON {TableNames.Erc20Transfer} (transaction_hash);

            -- index on the block_number column of all tables
            CREATE UNIQUE INDEX IF NOT EXISTS idx_block_block_number ON {TableNames.Block} (block_number);

            -- index on the 'timstamp' column of all tables
            CREATE INDEX IF NOT EXISTS idx_block_timestamp ON {TableNames.Block} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_signup_timestamp ON {TableNames.CirclesSignup} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_timestamp ON {TableNames.CirclesTrust} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_timestamp ON {TableNames.CirclesHubTransfer} (timestamp); 
            CREATE INDEX IF NOT EXISTS idx_erc20_transfer_timestamp ON {TableNames.Erc20Transfer} (timestamp);

            -- event specific indexes
            CREATE UNIQUE INDEX IF NOT EXISTS idx_circles_signup_user_address ON {TableNames.CirclesSignup} (circles_address);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_circles_signup_token_address ON {TableNames.CirclesSignup} (token_address);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_user_address ON {TableNames.CirclesTrust} (user_address);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_can_send_to_address ON {TableNames.CirclesTrust} (can_send_to_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_from_address ON {TableNames.CirclesHubTransfer} (from_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_to_address ON {TableNames.CirclesHubTransfer} (to_address);
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
            CREATE TABLE IF NOT EXISTS {TableNames.CirclesSignup} (
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
            CREATE TABLE IF NOT EXISTS {TableNames.CirclesTrust} (
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
            CREATE TABLE IF NOT EXISTS {TableNames.CirclesHubTransfer} (
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

        using NpgsqlCommand createCirclesTransferTableCmd = connection.CreateCommand();
        createCirclesTransferTableCmd.CommandText = @$"
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
        createCirclesTransferTableCmd.ExecuteNonQuery();
    }
}