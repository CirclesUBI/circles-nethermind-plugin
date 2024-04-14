using System.Numerics;
using Circles.Index.Indexer.Model;
using Npgsql;
using Nethermind.Int256;
using NpgsqlTypes;

namespace Circles.Index.Data.Postgresql;

public class Sink : IAsyncDisposable
{
    private readonly InsertBuffer<Block> _blockData = new();
    private readonly InsertBuffer<CirclesSignupData> _circlesSignupData = new();
    private readonly InsertBuffer<CirclesTrustData> _circlesTrustData = new();
    private readonly InsertBuffer<CirclesHubTransferData> _circlesHubTransferData = new();
    private readonly InsertBuffer<Erc20TransferData> _erc20TransferData = new();
    private readonly InsertBuffer<CrcV2ConvertInflationData> _crcV2ConvertInflationData = new();
    private readonly InsertBuffer<CrcV2InviteHumanData> _crcV2InviteHumanData = new();
    private readonly InsertBuffer<CrcV2PersonalMintData> _crcV2PersonalMintData = new();
    private readonly InsertBuffer<CrcV2RegisterGroupData> _crcV2RegisterGroupData = new();
    private readonly InsertBuffer<CrcV2RegisterHumanData> _crcV2RegisterHumanData = new();
    private readonly InsertBuffer<CrcV2RegisterOrganizationData> _crcV2RegisterOrganizationData = new();
    private readonly InsertBuffer<CrcV2TrustData> _crcV2TrustData = new();
    private readonly InsertBuffer<CrcV2StoppedData> _crcV2StoppedData = new();
    private readonly InsertBuffer<Erc1155TransferBatchData> _erc1155TransferBatchData = new();
    private readonly InsertBuffer<Erc1155TransferSingleData> _erc1155TransferSingleData = new();
    private readonly InsertBuffer<Erc1155ApprovalForAllData> _erc1155ApprovalForAllData = new();
    private readonly InsertBuffer<Erc1155UriData> _erc1155UriData = new();

    private readonly string _connectionString;

    public Sink(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void AddBlock(long blockNumber, long timestamp, string blockHash)
    {
        _blockData.Add(new(blockNumber, timestamp, blockHash));
    }

    public void AddCirclesSignup(long blockNumber, long timestamp, int transactionIndex, int logIndex,
        string transactionHash, string circlesAddress, string? tokenAddress)
    {
        _circlesSignupData.Add(new CirclesSignupData(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, circlesAddress,
            tokenAddress));
    }

    public void AddCirclesTrust(long blockNumber, long timestamp, int transactionIndex, int logIndex,
        string transactionHash, string userAddress, string canSendToAddress, int limit)
    {
        _circlesTrustData.Add(new(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, userAddress,
            canSendToAddress, limit));
    }

    public void AddCirclesHubTransfer(long blockNumber, long timestamp, int transactionIndex, int logIndex,
        string transactionHash, string fromAddress, string toAddress, UInt256 amount)
    {
        _circlesHubTransferData.Add(new(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, fromAddress,
            toAddress, amount
        ));
    }

    public void AddErc20Transfer(long blockNumber, long timestamp, int transactionIndex, int logIndex,
        string transactionHash, string tokenAddress, string from, string to, UInt256 value)
    {
        _erc20TransferData.Add(new(
            blockNumber, timestamp, transactionIndex,
            logIndex, transactionHash, tokenAddress, from,
            to, value
        ));
    }


    public void AddCrcV2RegisterOrganization(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, string orgAddress, string orgName)
    {
        _crcV2RegisterOrganizationData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, orgAddress,
            orgName
        ));
    }

    public void AddCrcV2RegisterGroup(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, string groupAddress, string mintPolicy, string treasury, string groupName,
        string groupSymbol)
    {
        _crcV2RegisterGroupData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, groupAddress,
            mintPolicy, treasury, groupName, groupSymbol
        ));
    }

    public void AddCrcV2RegisterHuman(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, string humanAddress)
    {
        _crcV2RegisterHumanData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, humanAddress
        ));
    }

    public void AddCrcV2PersonalMint(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, string toAddress, UInt256 amount, UInt256 startPeriod, UInt256 endPeriod)
    {
        _crcV2PersonalMintData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, toAddress, amount, startPeriod, endPeriod
        ));
    }

    public void AddCrcV2InviteHuman(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, string inviterAddress, string inviteeAddress)
    {
        _crcV2InviteHumanData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, inviterAddress, inviteeAddress
        ));
    }

    public void AddCrcV2ConvertInflation(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, UInt256 inflationValue, UInt256 demurrageValue, ulong day)
    {
        _crcV2ConvertInflationData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, inflationValue, demurrageValue, day
        ));
    }

    public void AddCrcV2Trust(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, string userAddress, string canSendToAddress, UInt256 limit)
    {
        _crcV2TrustData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, userAddress, canSendToAddress, limit
        ));
    }

    public void AddCrcV2Stopped(long blockNumber, long blockTimestamp, int receiptIndex, int logIndex,
        string transactionHash, string address)
    {
        _crcV2StoppedData.Add(new(
            blockNumber, blockTimestamp, receiptIndex,
            logIndex, transactionHash, address
        ));
    }

    public async Task Flush()
    {
        try
        {
            var tasks = new List<Task>
            {
                FlushBlocksBulk(),
                FlushCirclesSignupsBulk(),
                FlushCirclesTrustsBulk(),
                FlushCirclesHubTransfersBulk(),
                FlushCrcV2RegisterOrganizationBulk(),
                FlushCrcV2RegisterGroupBulk(),
                FlushCrcV2RegisterHumanBulk(),
                FlushCrcV2PersonalMintBulk(),
                FlushCrcV2InviteHumanBulk(),
                FlushCrcV2ConvertInflationBulk(),
                FlushCrcV2TrustBulk(),
                FlushCrcV2StoppedBulk(),
                FlushErc20TransfersBulk(),
                FlushErc1155TransferSingleBulk(),
                FlushErc1155TransferBatchBulk(),
                FlushErc1155ApprovalForAllBulk(),
                FlushErc1155UriBulk()
            };

            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task FlushErc1155UriBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.Erc1155Uri} (block_number, timestamp, transaction_index, log_index, transaction_hash, token_id, uri) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _erc1155UriData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.TokenId, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.URI, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushErc1155ApprovalForAllBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.Erc1155ApprovalForAll} (block_number, timestamp, transaction_index, log_index, transaction_hash, owner_address, operator_address, approved) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _erc1155ApprovalForAllData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.OwnerAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.OperatorAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.Approved, NpgsqlDbType.Boolean);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushErc1155TransferBatchBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.Erc1155TransferBatch} (block_number, timestamp, transaction_index, log_index, transaction_hash, operator_address, from_address, to_address, token_ids, amounts) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _erc1155TransferBatchData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.OperatorAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.FromAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.ToAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.TokenIds.Select(o => (BigInteger)o).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
            await writer.WriteAsync(item.Amounts.Select(o => (BigInteger)o).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Numeric);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushErc1155TransferSingleBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.Erc1155TransferSingle} (block_number, timestamp, transaction_index, log_index, transaction_hash, operator_address, from_address, to_address, token_id, amount) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _erc1155TransferSingleData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.OperatorAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.FromAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.ToAddress, NpgsqlDbType.Text);
            await writer.WriteAsync((BigInteger)item.TokenId, NpgsqlDbType.Numeric);
            await writer.WriteAsync((BigInteger)item.Amount, NpgsqlDbType.Numeric);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2StoppedBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2Stopped} (block_number, timestamp, transaction_index, log_index, transaction_hash, address) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2StoppedData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.Address, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2TrustBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2Trust} (block_number, timestamp, transaction_index, log_index, transaction_hash, truster_address, trustee_address, expiry_time) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2TrustData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.TrusterAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.TrusteeAddress, NpgsqlDbType.Text);
            await writer.WriteAsync((BigInteger)item.ExpiryTime, NpgsqlDbType.Numeric);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2ConvertInflationBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2ConvertInflation} (block_number, timestamp, transaction_index, log_index, transaction_hash, inflation_value, demurrage_value, \"day\") FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2ConvertInflationData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync((BigInteger)item.InflationValue, NpgsqlDbType.Numeric);
            await writer.WriteAsync((BigInteger)item.DemurrageValue, NpgsqlDbType.Numeric);
            await writer.WriteAsync(item.Day, NpgsqlDbType.Numeric);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2InviteHumanBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2InviteHuman} (block_number, timestamp, transaction_index, log_index, transaction_hash, inviter_address, invitee_address) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2InviteHumanData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.InviterAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.InviteeAddress, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2PersonalMintBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2PersonalMint} (block_number, timestamp, transaction_index, log_index, transaction_hash, to_address, amount, start_period, end_period) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2PersonalMintData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.ToAddress, NpgsqlDbType.Text);
            await writer.WriteAsync((BigInteger)item.Amount, NpgsqlDbType.Numeric);
            await writer.WriteAsync((BigInteger)item.StartPeriod, NpgsqlDbType.Numeric);
            await writer.WriteAsync((BigInteger)item.EndPeriod, NpgsqlDbType.Numeric);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2RegisterHumanBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2RegisterHuman} (block_number, timestamp, transaction_index, log_index, transaction_hash, address) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2RegisterHumanData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.Address, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2RegisterGroupBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2RegisterGroup} (block_number, timestamp, transaction_index, log_index, transaction_hash, group_address, group_mint_policy, group_treasury, group_name, group_symbol) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2RegisterGroupData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.GroupAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.GroupMintPolicy, NpgsqlDbType.Text);
            await writer.WriteAsync(item.GroupTreasury, NpgsqlDbType.Text);
            await writer.WriteAsync(item.GroupName, NpgsqlDbType.Text);
            await writer.WriteAsync(item.GroupSymbol, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCrcV2RegisterOrganizationBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV2RegisterOrganization} (block_number, timestamp, transaction_index, log_index, transaction_hash, organization_address, organization_name) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _crcV2RegisterOrganizationData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.OrganizationAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.OrganizationName, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushBlocksBulk()
    {
        await using var flushConnection = new NpgsqlConnection(_connectionString);
        flushConnection.Open();

        await using var writer =
            await flushConnection.BeginBinaryImportAsync(
                $"COPY {TableNames.Block} (block_number, timestamp, block_hash) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _blockData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.BlockHash, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCirclesSignupsBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV1Signup} (block_number, timestamp, transaction_index, log_index, transaction_hash, circles_address, token_address) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _circlesSignupData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.CirclesAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.TokenAddress ?? string.Empty, NpgsqlDbType.Text);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCirclesTrustsBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV1Trust} (" +
            $"block_number" +
            $", timestamp" +
            $", transaction_index" +
            $", log_index" +
            $", transaction_hash" +
            $", user_address" +
            $", can_send_to_address" +
            $", \"limit\") FROM STDIN (FORMAT BINARY)");

        foreach (var item in _circlesTrustData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.UserAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.CanSendToAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.Limit, NpgsqlDbType.Bigint);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushCirclesHubTransfersBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();

        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.CrcV1HubTransfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, from_address, to_address, amount) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _circlesHubTransferData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.FromAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.ToAddress, NpgsqlDbType.Text);
            await writer.WriteAsync((BigInteger)item.Amount, NpgsqlDbType.Numeric);
        }

        await writer.CompleteAsync();
    }

    private async Task FlushErc20TransfersBulk()
    {
        await using NpgsqlConnection flushConnection = new(_connectionString);
        await flushConnection.OpenAsync();
        
        await using var writer = await flushConnection.BeginBinaryImportAsync(
            $"COPY {TableNames.Erc20Transfer} (block_number, timestamp, transaction_index, log_index, transaction_hash, token_address, from_address, to_address, amount) FROM STDIN (FORMAT BINARY)");
        foreach (var item in _erc20TransferData.TakeSnapshot())
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(item.BlockNumber, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.Timestamp, NpgsqlDbType.Bigint);
            await writer.WriteAsync(item.TransactionIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.LogIndex, NpgsqlDbType.Integer);
            await writer.WriteAsync(item.TransactionHash, NpgsqlDbType.Text);
            await writer.WriteAsync(item.TokenAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.FromAddress, NpgsqlDbType.Text);
            await writer.WriteAsync(item.ToAddress, NpgsqlDbType.Text);
            await writer.WriteAsync((BigInteger)item.Amount, NpgsqlDbType.Numeric);
        }

        await writer.CompleteAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Flush();
    }
}