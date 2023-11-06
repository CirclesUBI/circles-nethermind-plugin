namespace Circles.Index.Data.Model;

public record CirclesTransferDto(
    long BlockNumber,
    string TransactionHash,
    string FromAddress,
    string ToAddress,
    string Amount,
    string TokenAddress,
    string Cursor
);
