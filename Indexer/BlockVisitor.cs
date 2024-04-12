using System.Globalization;
using Circles.Index.Data.Postgresql;
using Circles.Index.Utils;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Circles.Index.Indexer;

public class IndexerVisitor(Sink sink, Settings settings) : IIndexerVisitor
{
    #region Visitor

    public void VisitBlock(Block block)
    {
    }

    public bool VisitReceipt(Block block, TxReceipt receipt) => receipt.Logs != null;

    public bool VisitLog(Block block, TxReceipt receipt, LogEntry log, int logIndex) =>
        DispatchByTopic(block, receipt, log, logIndex);

    public void LeaveReceipt(Block block, TxReceipt receipt, bool logIndexed)
    {
    }

    public void LeaveBlock(Block block, bool receiptIndexed)
    {
        sink.AddBlock(block.Number, (long)block.Timestamp,
            block.Hash?.ToString() ?? throw new Exception("Block hash is null"));
    }

    #endregion

    #region Implementation

    private bool DispatchByTopic(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        if (log.Topics.Length == 0)
        {
            return false;
        }

        var topic = log.Topics[0];
        if (topic == StaticResources.Erc20TransferTopic &&
            Caches.CirclesTokenAddresses.ContainsKey(log.LoggersAddress))
        {
            return Erc20Transfer(block, receipt, log, logIndex);
        }

        if (log.LoggersAddress == settings.CirclesV1HubAddress)
        {
            if (topic == StaticResources.CrcSignupEventTopic)
            {
                return CrcSignup(block, receipt, log, logIndex);
            }

            if (topic == StaticResources.CrcHubTransferEventTopic)
            {
                return CrcHubTransfer(block, receipt, log, logIndex);
            }

            if (topic == StaticResources.CrcTrustEventTopic)
            {
                return CrcTrust(block, receipt, log, logIndex);
            }

            if (topic == StaticResources.CrcOrganisationSignupEventTopic)
            {
                return CrcOrgSignup(block, receipt, log, logIndex);
            }
        }

        return false;
    }

    private bool Erc20Transfer(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string from = "0x" + log.Topics[1].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);
        string to = "0x" + log.Topics[2].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);
        UInt256 value = new(log.Data, true);

        sink.AddErc20Transfer(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , log.LoggersAddress.ToString(true, false)
            , from
            , to
            , value);

        return true;
    }

    private bool CrcOrgSignup(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string user = "0x" + log.Topics[1].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);

        sink.AddCirclesSignup(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , user
            , null);

        return true;
    }

    private bool CrcTrust(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string user = "0x" + log.Topics[1].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);
        string canSendTo = "0x" + log.Topics[2].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);
        int limit = new UInt256(log.Data, true).ToInt32(CultureInfo.InvariantCulture);

        sink.AddCirclesTrust(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , user
            , canSendTo
            , limit);

        return true;
    }

    private bool CrcHubTransfer(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string from = "0x" + log.Topics[1].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);
        string to = "0x" + log.Topics[2].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);
        UInt256 amount = new(log.Data, true);

        sink.AddCirclesHubTransfer(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , from
            , to
            , amount);

        return true;
    }

    private bool CrcSignup(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string user = "0x" + log.Topics[1].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength);
        Address tokenAddress = new Address(log.Data.Slice(12));

        sink.AddCirclesSignup(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , user
            , tokenAddress.ToString(true, false));

        Caches.CirclesTokenAddresses.TryAdd(tokenAddress, null);

        // Every signup comes together with an Erc20 transfer (the signup bonus).
        // Since the signup event is emitted after the transfer, the token wasn't known yet when we encountered the transfer.
        // Look for the transfer again and process it.
        foreach (var repeatedLogEntry in receipt.Logs!)
        {
            if (repeatedLogEntry.LoggersAddress != tokenAddress)
            {
                continue;
            }

            if (repeatedLogEntry.Topics[0] == StaticResources.Erc20TransferTopic)
            {
                Erc20Transfer(block, receipt, repeatedLogEntry, logIndex);
            }
        }

        return true;
    }

    #endregion
}