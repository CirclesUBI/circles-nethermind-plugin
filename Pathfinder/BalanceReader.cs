using System.Data;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Data.Sqlite;
using Nethermind.Int256;

namespace Circles.Index.Pathfinder;

public class BalanceReader : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _queryString;
    private readonly IDictionary<string, uint> _addressIndexes;

    public BalanceReader(string connectionString, string queryString, IDictionary<string, uint> addressIndexes)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        _queryString = queryString;
        _addressIndexes = addressIndexes;
    }

    public async Task<IEnumerable<Balance>> ReadBalances(
        Stopwatch? queryStopWatch = null)
    {
        queryStopWatch?.Start();

        SqliteCommand cmd = new(_queryString, _connection);
        SqliteDataReader capacityReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        queryStopWatch?.Stop();

        return CreateBalanceReader(capacityReader);
    }

    private IEnumerable<Balance> CreateBalanceReader(SqliteDataReader capacityReader)
    {
        while (true)
        {
            bool end = !capacityReader.Read();
            if (end)
            {
                break;
            }

            string safeAddress = capacityReader.GetString(0).Substring(2);
            string tokenOwner = capacityReader.GetString(1).Substring(2);
            if (!_addressIndexes.TryGetValue(safeAddress, out uint safeAddressIdx)
             || !_addressIndexes.TryGetValue(tokenOwner, out uint tokenOwnerAddressIdx))
            {
                // Console.WriteLine($"Warning: Ignoring balance of address {safeAddress} with token {tokenOwner}");
                continue;
            }

            string balance = capacityReader.GetString(2);

            yield return new Balance(safeAddressIdx, tokenOwnerAddressIdx, UInt256.Parse(balance));
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
