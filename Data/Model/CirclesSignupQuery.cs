namespace Circles.Index.Data.Model;

public class CirclesSignupQuery
{
    public Range<long> BlockNumberRange { get; set; } = new();
    public string? TransactionHash { get; set; }
    public string? UserAddress { get; set; }
    public string? TokenAddress { get; set; }
    public long? Cursor { get; set; }
    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
}
