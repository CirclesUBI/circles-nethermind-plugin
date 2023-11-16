using Microsoft.Data.Sqlite;

namespace Circles.Index.Data.Sqlite;

public static class Schema
{
    public static void MigrateIndexes(SqliteConnection connection)
    {
        using SqliteCommand createIndexesCmd = connection.CreateCommand();
        createIndexesCmd.CommandText = @$"
            -- index on the transaction_hash column of all tables
            CREATE INDEX IF NOT EXISTS idx_circles_signup_transaction_hash ON {TableNames.CirclesSignup} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_transaction_hash ON {TableNames.CirclesTrust} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_transaction_hash ON {TableNames.CirclesHubTransfer} (transaction_hash);
            CREATE INDEX IF NOT EXISTS idx_circles_transfer_transaction_hash ON {TableNames.CirclesTransfer} (transaction_hash);

            -- index on the block_number column of all tables
            CREATE UNIQUE INDEX IF NOT EXISTS idx_block_relevant_block_number ON {TableNames.BlockRelevant} (block_number);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_block_irrelevant_block_number ON {TableNames.BlockIrrelevant} (block_number);

            -- index on the 'timstamp' column of all tables
            CREATE INDEX IF NOT EXISTS idx_block_relevant_timestamp ON {TableNames.BlockRelevant} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_block_irrelevant_timestamp ON {TableNames.BlockIrrelevant} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_signup_timestamp ON {TableNames.CirclesSignup} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_timestamp ON {TableNames.CirclesTrust} (timestamp);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_timestamp ON {TableNames.CirclesHubTransfer} (timestamp); 
            CREATE INDEX IF NOT EXISTS idx_circles_transfer_timestamp ON {TableNames.CirclesTransfer} (timestamp);

            -- event specific indexes
            CREATE UNIQUE INDEX IF NOT EXISTS idx_circles_signup_user_address ON {TableNames.CirclesSignup} (circles_address);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_circles_signup_token_address ON {TableNames.CirclesSignup} (token_address);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_user_address ON {TableNames.CirclesTrust} (user_address);
            CREATE INDEX IF NOT EXISTS idx_circles_trust_can_send_to_address ON {TableNames.CirclesTrust} (can_send_to_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_from_address ON {TableNames.CirclesHubTransfer} (from_address);
            CREATE INDEX IF NOT EXISTS idx_circles_hub_transfer_to_address ON {TableNames.CirclesHubTransfer} (to_address);
            CREATE INDEX IF NOT EXISTS idx_circles_transfer_from_address ON {TableNames.CirclesTransfer} (from_address);
            CREATE INDEX IF NOT EXISTS idx_circles_transfer_to_address ON {TableNames.CirclesTransfer} (to_address);
        ";
        createIndexesCmd.ExecuteNonQuery();
    }

    public static void MigrateTables(SqliteConnection connection)
    {
        using SqliteCommand createRelevantBlockTableCmd = connection.CreateCommand();
        createRelevantBlockTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.BlockRelevant} (
                block_number INTEGER PRIMARY KEY,
                timestamp INTEGER,
                block_hash TEXT
            );
        ";
        createRelevantBlockTableCmd.ExecuteNonQuery();

        using SqliteCommand createIrrelevantBlockTableCmd = connection.CreateCommand();
        createIrrelevantBlockTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.BlockIrrelevant} (
                block_number INTEGER PRIMARY KEY,
                timestamp INTEGER,
                block_hash TEXT
            );
        ";
        createIrrelevantBlockTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesSignupTableCmd = connection.CreateCommand();
        createCirclesSignupTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CirclesSignup} (
                block_number INTEGER,
                timestamp INTEGER,
                transaction_index INTEGER,
                log_index INTEGER,
                transaction_hash TEXT,
                circles_address TEXT,
                token_address TEXT NULL,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCirclesSignupTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesTrustTableCmd = connection.CreateCommand();
        createCirclesTrustTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CirclesTrust} (
                block_number INTEGER,
                timestamp INTEGER,
                transaction_index INTEGER,
                log_index INTEGER,
                transaction_hash TEXT,
                user_address TEXT,
                can_send_to_address TEXT,
                ""limit"" INTEGER,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCirclesTrustTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesHubTransferTableCmd = connection.CreateCommand();
        createCirclesHubTransferTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CirclesHubTransfer} (
                block_number INTEGER,
                timestamp INTEGER,
                transaction_index INTEGER,
                log_index INTEGER,
                transaction_hash TEXT,
                from_address TEXT,
                to_address TEXT,
                amount TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCirclesHubTransferTableCmd.ExecuteNonQuery();

        using SqliteCommand createCirclesTransferTableCmd = connection.CreateCommand();
        createCirclesTransferTableCmd.CommandText = @$"
            CREATE TABLE IF NOT EXISTS {TableNames.CirclesTransfer} (
                block_number INTEGER,
                timestamp INTEGER,
                transaction_index INTEGER,
                log_index INTEGER,
                transaction_hash TEXT,
                token_address TEXT,
                from_address TEXT,
                to_address TEXT,
                amount TEXT,
                PRIMARY KEY (block_number, transaction_index, log_index)
            );
        ";
        createCirclesTransferTableCmd.ExecuteNonQuery();
    }
}
