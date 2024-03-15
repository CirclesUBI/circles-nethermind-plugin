using System.Collections.Immutable;
using Circles.Index.Data.Cache;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Circles.Index.Indexer;

public record Context(
    string IndexDbLocation,
    ILogger Logger,
    long LastIndexHeight,
    long CurrentChainHeight,
    long LastReorgAt,
    ChainSpec ChainSpec,
    MemoryCache MemoryCache,
    CancellationTokenSource CancellationTokenSource,
    Settings Settings)
{
    public int PendingPathfinderUpdates;
    public ImmutableHashSet<long>? KnownBlocks { get; set; }
    public long MaxKnownBlock { get; set; }
    public long MinKnownBlock { get; set; }
    public long LastReorgAt { get; set; } = LastReorgAt;
    public long LastIndexHeight { get; set; } = LastIndexHeight;
    public List<Exception> Errors { get; } = new();
}