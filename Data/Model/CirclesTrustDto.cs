namespace Circles.Index.Data.Model;

public record CirclesTrustDto(
    long BlockNumber,
    string TransactionHash,
    string UserAddress,
    string CanSendToAddress,
    int Limit,
    string Cursor
);
