using System.Data;
using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Circles.Index.Pathfinder;

public class Users : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _queryString;
    private uint _idxCounter;
    public Dictionary<string, uint> UserAddressIndexes { get; }
    public Dictionary<string, uint> OrgAddressIndexes { get; }

    public Users(string connectionString, string queryString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        _queryString = queryString;

        UserAddressIndexes = new Dictionary<string, uint>();
        OrgAddressIndexes = new Dictionary<string, uint>();
    }

    public async Task Read(Stopwatch? queryStopWatch = null, Stopwatch? downloadStopWatch = null)
    {
        queryStopWatch?.Start();

        SqliteCommand cmd = new(_queryString, _connection);
        SqliteDataReader capacityReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        queryStopWatch?.Stop();
        downloadStopWatch?.Start();

        while (true)
        {
            bool end = !capacityReader.Read();
            if (end)
            {
                break;
            }

            string user = capacityReader.GetString(0).Substring(2);
            object token = capacityReader.GetValue(1);
            if (token is string s)
            {
                UserAddressIndexes.TryAdd(user, _idxCounter++);
            }
            else
            {
                UserAddressIndexes.TryAdd(user, _idxCounter);
                OrgAddressIndexes.TryAdd(user, _idxCounter++);
            };
        }

        downloadStopWatch?.Stop();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
