using Circles.Index.Data.Model;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Circles.Index.Rpc;

#region DTOs

public record TrustRelations(Address User, IReadOnlyDictionary<string, int> Trusts,
    IReadOnlyDictionary<string, int> TrustedBy);

public class CirclesTokenBalance
{
    public Address Token { get; }
    public string Balance { get;  }

    public CirclesTokenBalance(Address token, string balance)
    {
        Token = token;
        Balance = balance;
    }
}

#endregion

[RpcModule("Circles")]
public interface ICirclesRpcModule : IRpcModule
{
    [JsonRpcMethod(Description = "Gets the Circles balance of the specified address", IsImplemented = true)]
    Task<ResultWrapper<string>> circles_getTotalBalance(Address address);

    [JsonRpcMethod(Description = "Gets the balance of each Circles token the specified address holds", IsImplemented = true)]
    Task<ResultWrapper<CirclesTokenBalance[]>> circles_getTokenBalances(Address address);

    [JsonRpcMethod(Description = "Gets the Circles trust relations of the specified address", IsImplemented = true)]
    ResultWrapper<TrustRelations> circles_getTrustRelations(Address address);

    [JsonRpcMethod(Description = "Gets the Circles trust events as specified by the query", IsImplemented = true)]
    ResultWrapper<IEnumerable<CirclesTrustDto>> circles_queryTrustEvents(CirclesTrustQuery query);

    [JsonRpcMethod(Description = "Gets the hub transfer events as specified by the query", IsImplemented = true)]
    ResultWrapper<IEnumerable<CirclesHubTransferDto>> circles_queryHubTransfers(CirclesHubTransferQuery query);

    [JsonRpcMethod(Description = "Gets the Circles transfer events as specified by the query", IsImplemented = true)]
    ResultWrapper<IEnumerable<CirclesTransferDto>> circles_queryCrcTransfers(CirclesTransferQuery query);

    [JsonRpcMethod(Description = "Calculates a transitive transfer path along the trust relations of a user", IsImplemented = true)]
    ResultWrapper<string> circles_computeTransfer(string from, string to, string amount);

}
