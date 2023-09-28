using Nethermind.Api;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.Logging;

namespace Circles.Index;

public class CirclesRpcModule : ICirclesRpcModule
{
    private readonly IEthRpcModule? _ethRpcModule;
    private readonly SqlitePersistence _persistence;
    private readonly ILogger _pluginLogger;

    public CirclesRpcModule(INethermindApi nethermindApi, IEthRpcModule ethRpcModule, SqlitePersistence persistence)
    {
        ILogger baseLogger = nethermindApi.LogManager.GetClassLogger();
        _pluginLogger = new LoggerWithPrefix("Circles.Index.Rpc:", baseLogger);

        _ethRpcModule = ethRpcModule;
        _persistence = persistence;
    }

    public ResultWrapper<UInt256> circles_getTotalBalance(Address address)
    {
        if (_ethRpcModule == null)
        {
            return ResultWrapper<UInt256>.Success(new UInt256(0));
        }

        Address[] tokens = _persistence.GetTokenAddressesForAccount(address);

        // Call the erc20's balanceOf function for each token using _ethRpcModule.eth_call():
        byte[] functionSelector = Keccak.Compute("balanceOf(address)").Bytes.Slice(0, 4).ToArray();
        byte[] addressBytes = address.Bytes.PadLeft(32);
        byte[] data = functionSelector.Concat(addressBytes).ToArray();

        UInt256 totalBalance = UInt256.Zero;

        foreach (Address token in tokens)
        {
            TransactionForRpc transactionCall = new()
            {
                To = token,
                Data = data
            };

            ResultWrapper<string> result = _ethRpcModule.eth_call(transactionCall);
            if (result.ErrorCode != 0)
            {
                throw new Exception($"Couldn't get the balance of token {token} for account {address}");
            }

            byte[] uint256Bytes = Convert.FromHexString(result.Data.Substring(2));
            UInt256 tokenBalance = new(uint256Bytes, true);
            totalBalance += tokenBalance;
        }

        return ResultWrapper<UInt256>.Success(totalBalance);
    }

    public ResultWrapper<CirclesTokenBalance[]> circles_getTokenBalances(Address address)
    {
        if (_ethRpcModule == null)
        {
            return ResultWrapper<CirclesTokenBalance[]>.Success(Array.Empty<CirclesTokenBalance>());
        }

        Address[] tokens = _persistence.GetTokenAddressesForAccount(address);

        // Call the erc20's balanceOf function for each token using _ethRpcModule.eth_call():
        byte[] functionSelector = Keccak.Compute("balanceOf(address)").Bytes.Slice(0, 4).ToArray();
        byte[] addressBytes = address.Bytes.PadLeft(32);
        byte[] data = functionSelector.Concat(addressBytes).ToArray();

        List<CirclesTokenBalance> balances = new();

        foreach (Address token in tokens)
        {
            TransactionForRpc transactionCall = new()
            {
                To = token,
                Data = data
            };

            ResultWrapper<string> result = _ethRpcModule.eth_call(transactionCall);
            if (result.ErrorCode != 0)
            {
                throw new Exception($"Couldn't get the balance of token {token} for account {address}");
            }

            byte[] uint256Bytes = Convert.FromHexString(result.Data.Substring(2));
            UInt256 tokenBalance = new(uint256Bytes, true);

            balances.Add(new CirclesTokenBalance(token, tokenBalance));
        }

        return ResultWrapper<CirclesTokenBalance[]>.Success(balances.ToArray());
    }

    public ResultWrapper<TrustRelations> circles_getTrustRelations(Address address)
    {
        TrustRelations trustRelations =
            new(address, _persistence.GetTrusts(address), _persistence.GetTrustedBy(address));

        return ResultWrapper<TrustRelations>.Success(trustRelations);
    }

    public ResultWrapper<CirclesTransaction[]> circles_getHubTransfers(Address address)
    {
        CirclesTransaction[] transactions = _persistence.GetHubTransfers(address);
        return ResultWrapper<CirclesTransaction[]>.Success(transactions);
    }

    public ResultWrapper<CirclesTransaction[]> circles_getCrcTransfers(Address address)
    {
        CirclesTransaction[] transactions = _persistence.GetCrcTransfers(address);
        return ResultWrapper<CirclesTransaction[]>.Success(transactions);
    }

    public ResultWrapper<IEnumerable<TrustRelation>> circles_bulkGetTrustRelations()
    {
        return ResultWrapper<IEnumerable<TrustRelation>>.Success(_persistence.BulkGetTrustRelations());
    }

    public ResultWrapper<IEnumerable<UserSignup>> circles_bulkGetUsers()
    {
        return ResultWrapper<IEnumerable<UserSignup>>.Success(_persistence.BulkGetUsers());
    }

    public ResultWrapper<IEnumerable<Address>> circles_bulkGetOrganizations()
    {
        return ResultWrapper<IEnumerable<Address>>.Success(_persistence.BulkGetOrganizations());
    }
}
