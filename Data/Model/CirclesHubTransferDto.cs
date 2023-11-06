namespace Circles.Index.Data.Model;

public record CirclesHubTransferDto(
    string BlockNumber,
    string TransactionHash,
    string FromAddress,
    string ToAddress,
    string Amount,
    string Cursor
);
