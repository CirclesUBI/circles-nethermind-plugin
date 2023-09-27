using System.Globalization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Circles.Index;

public static class Indexer
{
    public static HashSet<long> IndexReceipts(TxReceipt[] receipts, SqlitePersistence persistence)
    {
        HashSet<long> relevantBlocks = new();

        foreach (TxReceipt txReceipt in receipts)
        {
            if (txReceipt.Logs == null)
                continue;

            foreach (LogEntry log in txReceipt.Logs)
            {
                if (log.Topics.Length == 0)
                    continue;

                Keccak topic = log.Topics[0];

                if (log.LoggersAddress == Settings.CirclesHubAddress)
                {
                    if (topic == StaticResources.CrcTrustEventTopic)
                    {
                        Address userAddress = new (
                            log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));
                        Address canSendToAddress = new (
                            log.Topics[2].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));
                        int limit = new UInt256(log.Data, true).ToInt32(CultureInfo.InvariantCulture);

                        persistence.AddCirclesTrust(txReceipt.BlockNumber, txReceipt.TxHash!.ToString(),
                            userAddress,
                            canSendToAddress,
                            limit);

                        relevantBlocks.Add(txReceipt.BlockNumber);
                    }
                    else if (topic == StaticResources.CrcHubTransferEventTopic)
                    {
                        Address fromAddress = new (
                            log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));
                        Address toAddress = new(log.Topics[2].ToString()
                            .Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));
                        UInt256 amount = new(log.Data, true);

                        persistence.AddCirclesHubTransfer(txReceipt.BlockNumber, txReceipt.TxHash!.ToString(),
                            fromAddress,
                            toAddress,
                            amount);

                        relevantBlocks.Add(txReceipt.BlockNumber);
                    }
                    else if (topic == StaticResources.CrcSignupEventTopic)
                    {
                        Address userAddress = new (
                            log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));
                        Address tokenAddress = new Address(log.Data.Slice(12));

                        persistence.AddCirclesSignup(txReceipt.BlockNumber, txReceipt.TxHash!.ToString(),
                            userAddress,
                            tokenAddress);

                        relevantBlocks.Add(txReceipt.BlockNumber);
                    }
                    else if (topic == StaticResources.CrcOrganisationSignupEventTopic)
                    {
                        Address userAddress = new (
                            log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));

                        persistence.AddCirclesSignup(txReceipt.BlockNumber, txReceipt.TxHash!.ToString(),
                            userAddress,
                            null);

                        relevantBlocks.Add(txReceipt.BlockNumber);
                    }
                }
                else if (topic == StaticResources.Erc20TransferTopic && persistence.IsCirclesToken(log.LoggersAddress))
                {
                    Address tokenAddress = new (log.LoggersAddress.ToString(true, false));
                    Address from = new (log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));
                    Address to = new (log.Topics[2].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x"));
                    UInt256 value = new(log.Data, true);

                    persistence.AddCirclesTransfer(txReceipt.BlockNumber, txReceipt.TxHash!.ToString(), tokenAddress,
                        from,
                        to,
                        value);

                    relevantBlocks.Add(txReceipt.BlockNumber);
                }
            }
        }

        return relevantBlocks;
    }
}
