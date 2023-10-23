using System.Globalization;
using Circles.Index.Data.Cache;
using Circles.Index.Data.Sqlite;
using Circles.Index.Utils;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Circles.Index.Indexer;

public static class ReceiptIndexer
{
    public static HashSet<(long BlockNo, Keccak BlockHash)> IndexReceipts(TxReceipt[] receipts, Settings settings, MemoryCache cache,
        Sink persistence)
    {
        HashSet<(long, Keccak)> relevantBlocks = new();

        foreach (TxReceipt txReceipt in receipts)
        {
            if (txReceipt.Logs == null)
                continue;

            for (int i = 0; i < txReceipt.Logs.Length; i++)
            {
                LogEntry log = txReceipt.Logs[i];

                if (log.Topics.Length == 0)
                    continue;

                Keccak topic = log.Topics[0];

                string loggersAddressStr = log.LoggersAddress.ToString(true, false);
                if (log.LoggersAddress == settings.CirclesHubAddress)
                {
                    if (topic == StaticResources.CrcTrustEventTopic)
                    {
                        string userAddress = log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x");
                        string canSendToAddress = log.Topics[2].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x");
                        int limit = new UInt256(log.Data, true).ToInt32(CultureInfo.InvariantCulture);

                        persistence.AddCirclesTrust(txReceipt.BlockNumber, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            userAddress,
                            canSendToAddress,
                            limit);

                        relevantBlocks.Add((txReceipt.BlockNumber, txReceipt.BlockHash!));
                        cache.TrustGraph.AddOrUpdateEdge(userAddress, canSendToAddress, limit);
                    }
                    else if (topic == StaticResources.CrcHubTransferEventTopic)
                    {
                        string fromAddress = log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x");
                        string toAddress = log.Topics[2].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x");
                        UInt256 amount = new(log.Data, true);

                        persistence.AddCirclesHubTransfer(txReceipt.BlockNumber, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            fromAddress,
                            toAddress,
                            amount.ToString(CultureInfo.InvariantCulture));

                        relevantBlocks.Add((txReceipt.BlockNumber, txReceipt.BlockHash!));
                    }
                    else if (topic == StaticResources.CrcSignupEventTopic)
                    {
                        string userAddress = log.Topics[1].ToString()
                            .Replace(StaticResources.AddressEmptyBytesPrefix, "0x");
                        string tokenAddress = new Address(log.Data.Slice(12)).ToString(true, false);

                        persistence.AddCirclesSignup(txReceipt.BlockNumber, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            userAddress,
                            tokenAddress);

                        relevantBlocks.Add((txReceipt.BlockNumber, txReceipt.BlockHash!));
                        cache.SignupCache.Add(userAddress, tokenAddress);
                    }
                    else if (topic == StaticResources.CrcOrganisationSignupEventTopic)
                    {
                        string userAddress = log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x");

                        persistence.AddCirclesSignup(txReceipt.BlockNumber, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            userAddress,
                            null);

                        relevantBlocks.Add((txReceipt.BlockNumber, txReceipt.BlockHash!));
                        cache.SignupCache.Add(userAddress, null);
                    }
                }
                else if (topic == StaticResources.Erc20TransferTopic && cache.IsCirclesToken(loggersAddressStr))
                {
                    string from = log.Topics[1].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x");
                    string to = log.Topics[2].ToString().Replace(StaticResources.AddressEmptyBytesPrefix, "0x");
                    UInt256 value = new(log.Data, true);

                    persistence.AddCirclesTransfer(txReceipt.BlockNumber, txReceipt.Index, i,
                        txReceipt.TxHash!.ToString(),
                        loggersAddressStr,
                        from,
                        to,
                        value);

                    relevantBlocks.Add((txReceipt.BlockNumber, txReceipt.BlockHash!));
                }
            }
        }

        return relevantBlocks;
    }
}
