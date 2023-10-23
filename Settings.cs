using Nethermind.Core;

namespace Circles.Index;

public class Settings
{
    public readonly string DbFileName = "circles-index.sqlite";

    public readonly Address CirclesHubAddress = new("0x29b9a7fBb8995b2423a71cC17cf9810798F6C543");
    public ulong ChainId { get; set; } = 100;

    public int MaxParallelism { get; set; } = 0;
}
