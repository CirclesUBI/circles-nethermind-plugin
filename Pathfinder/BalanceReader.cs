using System.Numerics;
using Circles.Index.Data.Cache;
using Microsoft.Data.Sqlite;
using Nethermind.Int256;

namespace Circles.Index.Pathfinder;

public static class BalanceReader
{
    public static async Task<IEnumerable<Balance>> Read(
        string dbLocation,
        MemoryCache cache)
    {
        await using var connection = new SqliteConnection($"Data Source={dbLocation}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
                SELECT
                    from_address,
                    to_address,
                    token_address,
                    amount
                FROM circles_transfer
                order by block_number, transaction_index, log_index;";

        var balances = new Dictionary<(string Address, string TokenAddress), BigInteger>();
        await using var reader = await command.ExecuteReaderAsync();

        var zero = "0x0000000000000000000000000000000000000000";
        
        while (await reader.ReadAsync())
        {
            string fromAddress = reader.GetString(0);
            string toAddress = reader.GetString(1);
            string tokenAddress = reader.GetString(2);
            BigInteger amount = BigInteger.Parse(reader.GetString(3));

            (string fromAddress, string tokenAddress) fromKey = (fromAddress, tokenAddress);
            balances.TryAdd(fromKey, BigInteger.Zero);
            if (fromAddress != zero)
            {
                balances[fromKey] -= amount;
            }

            (string toAddress, string tokenAddress) toKey = (toAddress, tokenAddress);
            balances.TryAdd(toKey, BigInteger.Zero);
            balances[toKey] += amount;
        }

        // Convert to list of Balance objects
        var balanceList = new List<Balance>();
        foreach (var entry in balances)
        {
            string tokenOwnerAddress = cache.SignupCache.GetTokenOwner(entry.Key.TokenAddress);
 
            if (!cache.SignupCache.AllUserIndexes.TryGetValue(entry.Key.Address, out uint userIndex))
            {
                // TODO: This doesn't include the ERC20 transfers of CRC tokens. Only transfers of CRC users are included.
                continue;
            }
            
            uint tokenOwnerIndex = cache.SignupCache.AllUserIndexes[tokenOwnerAddress];
            if (entry.Value < 0)
            {
                Console.WriteLine(
                    $"user: {userIndex}, tokenOwner: {tokenOwnerIndex}, value: {entry.Value}");
            }
            
            UInt256 uint256 = new UInt256(entry.Value.ToByteArray(true, true), true);
            balanceList.Add(new Balance(userIndex, tokenOwnerIndex, uint256));
        }

        return balanceList;
    }
}