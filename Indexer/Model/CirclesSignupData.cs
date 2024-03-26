namespace Circles.Index.Indexer.Model;

public record CirclesSignupData(
    long BlockNumber,
    ulong Timestamp,
    int TransactionIndex,
    int LogIndex,
    string TransactionHash,
    string CirclesAddress,
    string? TokenAddress);