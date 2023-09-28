using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Circles.Index;

public class TrustRelations
{
    public Address User { get; }

    public IReadOnlyDictionary<Address, int> Trusts { get; }
    public IReadOnlyDictionary<Address, int> TrustedBy { get; }

    public TrustRelations(Address user, IReadOnlyDictionary<Address, int> trusts, IReadOnlyDictionary<Address, int> trustedBy)
    {
        User = user;
        Trusts = trusts;
        TrustedBy = trustedBy;
    }
}

public class TrustRelation
{
    public Address User { get; }
    public Address CanSendTo { get; }
    public int Limit { get; }

    public TrustRelation(Address user, Address canSendTo, int limit)
    {
        User = user;
        CanSendTo = canSendTo;
        Limit = limit;
    }
}

public class UserSignup
{
    public Address UserAddress { get; }
    public Address TokenAddress { get; }

    public UserSignup(Address userAddress, Address tokenAddress)
    {
        UserAddress = userAddress;
        TokenAddress = tokenAddress;
    }
}

public class CirclesTokenBalance
{
    public Address Token { get; }
    public UInt256 Balance { get;  }

    public CirclesTokenBalance(Address token, UInt256 balance)
    {
        Token = token;
        Balance = balance;
    }
}

public class CirclesTransaction
{
    public long Block { get; }
    public Keccak TransactionHash { get; }
    public Address? Token { get; }
    public Address From { get; }
    public Address To { get; }
    public UInt256 Amount { get; }

    public CirclesTransaction(long blockNumber, string transactionHash, Address? token, Address fromAddress, Address toAddress, UInt256 amount)
    {
        Block = blockNumber;
        TransactionHash = new Keccak(transactionHash);
        Token = token;
        From = fromAddress;
        To = toAddress;
        Amount = amount;
    }
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

    [JsonRpcMethod(Description = "Gets the hub transfers from or to the specified address", IsImplemented = true)]
    ResultWrapper<CirclesTransaction[]> circles_getHubTransfers(Address address);

    [JsonRpcMethod(Description = "Gets all Circles transactions from or to the specified address", IsImplemented = true)]
    ResultWrapper<CirclesTransaction[]> circles_getCrcTransfers(Address address);

    [JsonRpcMethod(Description = "Gets all Circles trust relations", IsImplemented = true)]
    ResultWrapper<IEnumerable<TrustRelation>> circles_bulkGetTrustRelations();

    [JsonRpcMethod(Description = "Gets all Circles users and their token", IsImplemented = true)]
    ResultWrapper<IEnumerable<UserSignup>> circles_bulkGetUsers();

    [JsonRpcMethod(Description = "Gets all Circles organizations", IsImplemented = true)]
    ResultWrapper<IEnumerable<Address>> circles_bulkGetOrganizations();
}
