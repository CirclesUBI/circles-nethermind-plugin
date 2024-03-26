using Circles.Index.Indexer;
using Nethermind.Core;

namespace Circles.Index;

public class Settings
{
    public readonly string IndexDbFileName = "circles-index.sqlite";

    public readonly Address CirclesHubAddress = new("0x29b9a7fBb8995b2423a71cC17cf9810798F6C543");

    public readonly string PathfinderRpcUrl = "http://localhost:8080";

    public readonly string PathfinderDbFileName = "pathfinder.db";

    public static readonly int InitialUserCacheSize = 150000;

    public static readonly int InitialOrgCacheSize = 15000;

    public ulong ChainId { get; set; } = 100;
}
