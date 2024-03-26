namespace Circles.Index.Indexer.Model;

public record CirclesTrustData(
    long BlockNumber,
    ulong Timestamp,
    int TransactionIndex,
    int LogIndex,
    string TransactionHash,
    string UserAddress,
    string CanSendToAddress,
    int Limit);