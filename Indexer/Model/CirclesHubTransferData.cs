using Nethermind.Int256;

public record CirclesHubTransferData(
    long BlockNumber,
    ulong Timestamp,
    int TransactionIndex,
    int LogIndex,
    string TransactionHash,
    string FromAddress,
    string ToAddress,
    UInt256 Amount);