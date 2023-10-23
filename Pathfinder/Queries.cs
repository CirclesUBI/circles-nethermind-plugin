namespace Circles.Index.Pathfinder;

public static class Queries
{
    public const string BalancesBySafeAndToken = @"
        select safe_address, token_owner, balance::text
        from cache_crc_balances_by_safe_and_token
        where safe_address != '0x0000000000000000000000000000000000000000'
        and balance > 0;
    ";
}
