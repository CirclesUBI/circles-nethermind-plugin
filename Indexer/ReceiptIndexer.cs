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
    public static HashSet<(long BlockNo, ulong Timestamp, Hash256 BlockHash)> IndexReceipts((long blockNo, ulong timestamp, Hash256 blockHash, TxReceipt[] receipts) data, Settings settings, MemoryCache cache,
        Sink persistence)
    {
        HashSet<(long, ulong, Hash256)> relevantBlocks = new();
        Dictionary<LogEntry, int> erc20TransferLogs = new();

        foreach (TxReceipt txReceipt in data.receipts)
        {
            if (txReceipt.Logs == null)
                continue;

            for (int i = 0; i < txReceipt.Logs.Length; i++)
            {
                LogEntry log = txReceipt.Logs[i];

                if (log.Topics.Length == 0)
                    continue;

                Hash256 topic = log.Topics[0];

                if (log.LoggersAddress == settings.CirclesHubAddress)
                {
                    if (topic == StaticResources.CrcTrustEventTopic)
                    {
                        string userAddress = $"0x{log.Topics[1].ToString()
                            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
                        string canSendToAddress = $"0x{log.Topics[2].ToString()
                            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
                        int limit = new UInt256(log.Data, true).ToInt32(CultureInfo.InvariantCulture);

                        persistence.AddCirclesTrust(txReceipt.BlockNumber, data.timestamp, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            userAddress,
                            canSendToAddress,
                            limit);

                        relevantBlocks.Add((txReceipt.BlockNumber, data.timestamp, txReceipt.BlockHash!));
                        cache.TrustGraph.AddOrUpdateEdge(userAddress, canSendToAddress, limit);
                    }
                    else if (topic == StaticResources.CrcHubTransferEventTopic)
                    {
                        string fromAddress = $"0x{log.Topics[1].ToString()
                            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
                        string toAddress = $"0x{log.Topics[2].ToString()
                            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
                        UInt256 amount = new(log.Data, true);

                        persistence.AddCirclesHubTransfer(txReceipt.BlockNumber, data.timestamp, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            fromAddress,
                            toAddress,
                            amount.ToString(CultureInfo.InvariantCulture));

                        relevantBlocks.Add((txReceipt.BlockNumber, data.timestamp, txReceipt.BlockHash!));
                    }
                    else if (topic == StaticResources.CrcSignupEventTopic)
                    {
                        string userAddress =$"0x{ log.Topics[1].ToString()
                            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
                        string tokenAddress = new Address(log.Data.Slice(12)).ToString(true, false);

                        persistence.AddCirclesSignup(txReceipt.BlockNumber, data.timestamp, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            userAddress,
                            tokenAddress);

                        relevantBlocks.Add((txReceipt.BlockNumber, data.timestamp, txReceipt.BlockHash!));
                        cache.SignupCache.Add(userAddress, tokenAddress);
                    }
                    else if (topic == StaticResources.CrcOrganisationSignupEventTopic)
                    {
                        string userAddress = $"0x{log.Topics[1].ToString()
                            .Substring(StaticResources.AddressEmptyBytesPrefixLength)}";

                        persistence.AddCirclesSignup(txReceipt.BlockNumber, data.timestamp, txReceipt.Index, i,
                            txReceipt.TxHash!.ToString(),
                            userAddress,
                            null);

                        relevantBlocks.Add((txReceipt.BlockNumber, data.timestamp, txReceipt.BlockHash!));
                        cache.SignupCache.Add(userAddress, null);
                    }
                }
                else if (topic == StaticResources.Erc20TransferTopic)
                {
                    // Need to buffer all erc20 transfers because
                    // we cannot know yet if it's a CRC token.
                    // Signup events occur after the first transfer (signup bonus)
                    // and we only know the CRC address after the signup event.
                    erc20TransferLogs.Add(log, i);
                }
            }

            foreach ((LogEntry logEntry, int logIndex) in erc20TransferLogs)
            {
                string loggersAddressStr = logEntry.LoggersAddress.ToString(true, false);
                if (!cache.IsCirclesToken(loggersAddressStr))
                {
                    continue;
                }

                string from = $"0x{logEntry.Topics[1].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
                string to = $"0x{logEntry.Topics[2].ToString().Substring(StaticResources.AddressEmptyBytesPrefixLength)}";
                UInt256 value = new(logEntry.Data, true);

                persistence.AddCirclesTransfer(txReceipt.BlockNumber, data.timestamp, txReceipt.Index, logIndex,
                    txReceipt.TxHash!.ToString(),
                    loggersAddressStr,
                    from,
                    to,
                    value);

                relevantBlocks.Add((txReceipt.BlockNumber, data.timestamp, txReceipt.BlockHash!));
                
                // if (from != StateMachine._zeroAddress)
                // {
                //     cache.Balances.Out(from, loggersAddressStr, value);
                // }
                // cache.Balances.In(to, loggersAddressStr, value);
            }

            erc20TransferLogs.Clear();
        }

        return relevantBlocks;
    }
}
