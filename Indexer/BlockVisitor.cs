using System.Globalization;
using Circles.Index.Data.Sqlite;
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
        throw new NotImplementedException();
    }

    public void LeaveBlock(Block block, bool receiptIndexed)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Implementation

    private bool DispatchByTopic(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        if (log.Topics[0] == StaticResources.Erc20TransferTopic)
        {
            return Erc20Transfer(block, receipt, log, logIndex);
        }

        if (log.LoggersAddress == settings.CirclesHubAddress)
        {
            if (log.Topics[0] == StaticResources.CrcSignupEventTopic)
            {
                return CrcSignup(block, receipt, log, logIndex);
            }

            if (log.Topics[0] == StaticResources.CrcHubTransferEventTopic)
            {
                return CrcHubTransfer(block, receipt, log, logIndex);
            }

            if (log.Topics[0] == StaticResources.CrcTrustEventTopic)
            {
                return CrcTrust(block, receipt, log, logIndex);
            }

            if (log.Topics[0] == StaticResources.CrcOrganisationSignupEventTopic)
            {
                return CrcOrgSignup(block, receipt, log, logIndex);
            }
        }

        return false;
    }

    private bool Erc20Transfer(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string from =
            $"0x{log.Topics[1].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
        string to =
            $"0x{log.Topics[2].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
        UInt256 value = new(log.Data, true);
        
        sink.AddErc20Transfer(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , from
            , to
            , value.ToString(CultureInfo.InvariantCulture));
        
        return true;
    }

    private bool CrcOrgSignup(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string userAddress = $"0x{log.Topics[1].ToString()
            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";

        sink.AddCirclesSignup(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , userAddress
            , null);

        return true;
    }

    private bool CrcTrust(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string userAddress = $"0x{log.Topics[1].ToString()
            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
        string canSendToAddress = $"0x{log.Topics[2].ToString()
            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
        int limit = new UInt256(log.Data, true).ToInt32(CultureInfo.InvariantCulture);

        sink.AddCirclesTrust(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , userAddress
            , canSendToAddress
            , limit);

        return true;
    }

    private bool CrcHubTransfer(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string fromAddress = $"0x{log.Topics[1].ToString()
            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
        string toAddress = $"0x{log.Topics[2].ToString()
            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
        UInt256 amount = new(log.Data, true);

        sink.AddCirclesHubTransfer(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , fromAddress
            , toAddress
            , amount.ToString(CultureInfo.InvariantCulture));

        return true;
    }

    private bool CrcSignup(Block block, TxReceipt receipt, LogEntry log, int logIndex)
    {
        string userAddress = $"0x{log.Topics[1].ToString()
            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
        string tokenAddress = new Address(log.Data.Slice(12))
            .ToString(true, false);

        sink.AddCirclesSignup(
            receipt.BlockNumber
            , block.Timestamp
            , receipt.Index
            , logIndex
            , receipt.TxHash!.ToString()
            , userAddress
            , tokenAddress);

        return true;
    }

    #endregion
}