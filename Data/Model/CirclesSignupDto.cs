namespace Circles.Index.Data.Model;

public record CirclesSignupDto(
    long BlockNumber,
    string TransactionHash,
    string CirclesAddress,
    string? TokenAddress,
    string Cursor
);
