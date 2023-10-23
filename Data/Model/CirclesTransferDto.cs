using Nethermind.Int256;

namespace Circles.Index.Data.Model;

public record CirclesTransferDto(
    long BlockNumber,
    string TransactionHash,
    string FromAddress,
    string ToAddress,
    UInt256 Amount,
    string TokenAddress
);
