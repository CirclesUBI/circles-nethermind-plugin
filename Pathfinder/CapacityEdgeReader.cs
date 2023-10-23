using System.Data;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Data.Sqlite;

namespace Circles.Index.Pathfinder;

public class CapacityEdgeReader : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _queryString;

    public CapacityEdgeReader(string connectionString, string queryString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        _queryString = queryString;
    }

    public async Task<IEnumerable<(
            string senderAddress,
            string receiverAddress,
            string tokenOwnerAddress,
            BigInteger capacity
        )>> ReadCapacityEdges(
        Stopwatch? queryStopWatch = null)
    {
        queryStopWatch?.Start();

        SqliteCommand cmd = new(_queryString, _connection);
        SqliteDataReader capacityReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        queryStopWatch?.Stop();

        return CreateCapacityEdgeReader(capacityReader);
    }

    private IEnumerable<(
            string senderAddress,
            string receiverAddress,
            string tokenOwnerAddress,
            BigInteger capacity
        )> CreateCapacityEdgeReader(
        IDataReader capacityReader)
    {
        while (true)
        {
            var end = !capacityReader.Read();
            if (end)
            {
                break;
            }

            var sender = capacityReader.GetString(0).Substring(2);
            var receiver = capacityReader.GetString(1).Substring(2);
            var tokenOwner = capacityReader.GetString(2).Substring(2);
            var capacity = capacityReader.GetString(3);

            // We might get a decimal number. If so, use only the part before the decimal point.
            var capacityBigInteger = ParsePgBigInt(capacity);

            yield return (
                sender,
                receiver,
                tokenOwner,
                capacityBigInteger);
        }
    }

    public static BigInteger ParsePgBigInt(string str)
    {
        var decimalPointIndex = str.IndexOf(".", StringComparison.Ordinal);
        if (decimalPointIndex > -1)
        {
            str = str.Substring(0, decimalPointIndex);
        }

        if (!BigInteger.TryParse(str, out var capacityBigInteger))
        {
            throw new Exception($"Couldn't parse string {str} as BigInteger value.");
        }

        return capacityBigInteger;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
