namespace Circles.Index.Data.Model;

public record CirclesTrustDto(
    string BlockNumber,
    string TransactionHash,
    string UserAddress,
    string CanSendToAddress,
    int Limit,
    string Cursor
);
