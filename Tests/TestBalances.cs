using System.Collections.Concurrent;
using System.Globalization;
using Circles.Index.Data.Cache;
using Circles.Index.Rpc;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.Logging;

namespace Circles.Index.Tests;

public class TestBalances
{
    public static void Test(string dbLocation, IEthRpcModule rpcModule, MemoryCache memoryCache, ILogger logger)
    {
        // Iterate over all accounts
        foreach (string user in memoryCache.SignupCache.AllUserIndexes.Keys)
        {
            // Get the balance from the cache
            if (!memoryCache.Balances.BalancesPerAccountAndToken.TryGetValue(user, out ConcurrentDictionary<string, UInt256>? cacheBalance))
            {
                // No balance for this account.
                // If this is a personal account there must be at least a balance entry. Even if it is '0'.
                
                logger.Error($"Account {user} has no balance entries in the cache.");
                continue;
            }
            
            // Get the balance from the chain
            var rpcBalances = CirclesRpcModule.CirclesTokenBalances(dbLocation, rpcModule, new Address(user), logger);
            
            
            // Compare the balances
            foreach (var rpcBalance in rpcBalances)
            {
                if (!cacheBalance.TryGetValue(rpcBalance.Token.ToString(true, false), out UInt256 cacheValue))
                {
                    logger.Error($"Account {user} Token {rpcBalance.Token} not found in cache");
                }

                if (cacheValue.ToString(CultureInfo.InvariantCulture) != rpcBalance.Balance)
                {
                    logger.Error($"Balance mismatch for user {user} and token {rpcBalance.Token}: {cacheValue} != {rpcBalance.Balance}");
                }
            }
        }
    }
}