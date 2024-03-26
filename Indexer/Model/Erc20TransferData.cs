using Nethermind.Int256;

namespace Circles.Index.Indexer.Model;

public record Erc20TransferData(
    long BlockNumber,
    ulong Timestamp,
    int TransactionIndex,
    int LogIndex,
    string TransactionHash,
    string TokenAddress,
    string FromAddress,
    string ToAddress,
    UInt256 Amount);