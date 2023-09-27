using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Circles.Index;

public class TrustRelations
{
    public Address User { get; set; }

    public IDictionary<Address, int> Trusts { get; set; }
    public IDictionary<Address, int> TrustedBy { get; set; }
}

public class CirclesTokenBalance
{
    public Address Token { get; set; }
    public UInt256 Balance { get; set; }
}

public class CirclesTransaction
{
    public long Block { get; set; }
    public Keccak TransactionHash { get; set; }
    public Address From { get; set; }
    public Address To { get; set; }
    public UInt256 Amount { get; set; }
}

[RpcModule("Circles")]
public interface ICirclesRpcModule : IRpcModule
{
    [JsonRpcMethod(Description = "Gets the Circles balance of the specified address", IsImplemented = true)]
    ResultWrapper<UInt256> circles_getTotalBalance(Address address);

    [JsonRpcMethod(Description = "Gets the balance of each Circles token the specified address holds", IsImplemented = true)]
    ResultWrapper<CirclesTokenBalance[]> circles_getTokenBalances(Address address);

    [JsonRpcMethod(Description = "Gets the Circles trust relations of the specified address", IsImplemented = true)]
    ResultWrapper<TrustRelations> circles_getTrustRelations(Address address);

    [JsonRpcMethod(Description = "Gets the Circles transactions of the specified address", IsImplemented = true)]
    ResultWrapper<CirclesTransaction[]> circles_getTransactionHistory(Address address);
}
