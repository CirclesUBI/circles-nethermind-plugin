using System.Globalization;
using Circles.Index.Data.Cache;
using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
using Circles.Index.Pathfinder;
using Circles.Index.Utils;
using Microsoft.Data.Sqlite;
using Nethermind.Api;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.Logging;

namespace Circles.Index.Rpc;

public class CirclesRpcModule : ICirclesRpcModule
{
    private readonly ILogger _pluginLogger;
    private readonly INethermindApi _nethermindApi;

    private readonly string _dbLocation;
    private readonly MemoryCache _cache;

    public CirclesRpcModule(INethermindApi nethermindApi, MemoryCache cache, string dbLocation)
    {
        ILogger baseLogger = nethermindApi.LogManager.GetClassLogger();
        _nethermindApi = nethermindApi;
        _pluginLogger = new LoggerWithPrefix("Circles.Index.Rpc:", baseLogger);

        _dbLocation = dbLocation;
        _cache = cache;
    }
    
    public async Task<ResultWrapper<string>> circles_getTotalBalance(Address address)
    {
        using RentedEthRpcModule rentedEthRpcModule = new(_nethermindApi);
        await rentedEthRpcModule.Rent();
        
        UInt256 totalBalance = TotalBalance(_dbLocation, rentedEthRpcModule.RpcModule!, address, _pluginLogger);
        return ResultWrapper<string>.Success(totalBalance.ToString(CultureInfo.InvariantCulture));
    }

    public static UInt256 TotalBalance(string dbLocation, IEthRpcModule rpcModule, Address address, ILogger? logger)
    {
        using SqliteConnection connection = new($"Data Source={dbLocation}");
        connection.Open();
        //logger?.Info("circles_getTotalBalance: Query connection opened");

        IEnumerable<Address> tokens = Query.TokenAddressesForAccount(connection, address);

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
                Input = data
            };

            ResultWrapper<string> result = rpcModule.eth_call(transactionCall);
            if (result.ErrorCode != 0)
            {
                throw new Exception($"Couldn't get the balance of token {token} for account {address}");
            }

            byte[] uint256Bytes = Convert.FromHexString(result.Data.Substring(2));
            UInt256 tokenBalance = new(uint256Bytes, true);
            totalBalance += tokenBalance;
        }

        return totalBalance;
    }

    public async Task<ResultWrapper<CirclesTokenBalance[]>> circles_getTokenBalances(Address address)
    {
        using RentedEthRpcModule rentedEthRpcModule = new(_nethermindApi);
        await rentedEthRpcModule.Rent();

        var balances = CirclesTokenBalances(_dbLocation, rentedEthRpcModule.RpcModule!, address, _pluginLogger);

        return ResultWrapper<CirclesTokenBalance[]>.Success(balances.ToArray());
    }

    public static List<CirclesTokenBalance> CirclesTokenBalances(string dbLocation, IEthRpcModule rpcModule, Address address, ILogger? logger)
    {
        using SqliteConnection connection = new($"Data Source={dbLocation}");
        connection.Open();
        //logger?.Info("circles_getTokenBalances: Query connection opened");
        IEnumerable<Address> tokens = Query.TokenAddressesForAccount(connection, address);

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
                Input = data
            };

            ResultWrapper<string> result = rpcModule.eth_call(transactionCall);
            if (result.ErrorCode != 0)
            {
                throw new Exception($"Couldn't get the balance of token {token} for account {address}");
            }

            byte[] uint256Bytes = Convert.FromHexString(result.Data.Substring(2));
            UInt256 tokenBalance = new(uint256Bytes, true);

            balances.Add(new CirclesTokenBalance(token, tokenBalance.ToString(CultureInfo.InvariantCulture)));
        }

        return balances;
    }

    public ResultWrapper<TrustRelations> circles_getTrustRelations(Address address)
    {
        return !_cache.TrustGraph.TryGet(address.ToString(true, false), out TrustRelations? trustRelations)
            ? ResultWrapper<TrustRelations>.Fail("Couldn't get the trust relations")
            : ResultWrapper<TrustRelations>.Success(trustRelations!);
    }

    public ResultWrapper<IEnumerable<CirclesTrustDto>> circles_queryTrustEvents(CirclesTrustQuery query)
    {
        SqliteConnection connection = new($"Data Source={_dbLocation}");
        connection.Open();

        IEnumerable<CirclesTrustDto> result = Query.CirclesTrusts(connection, query, true);
        return ResultWrapper<IEnumerable<CirclesTrustDto>>.Success(result);
    }

    public ResultWrapper<IEnumerable<CirclesHubTransferDto>> circles_queryHubTransfers(CirclesHubTransferQuery query)
    {
        SqliteConnection connection = new($"Data Source={_dbLocation}");
        connection.Open();

        IEnumerable<CirclesHubTransferDto> result = Query.CirclesHubTransfers(connection, query, true);
        return ResultWrapper<IEnumerable<CirclesHubTransferDto>>.Success(result);
    }

    public ResultWrapper<IEnumerable<CirclesTransferDto>> circles_queryCrcTransfers(CirclesTransferQuery query)
    {
        SqliteConnection connection = new($"Data Source={_dbLocation}");
        connection.Open();

        IEnumerable<CirclesTransferDto> result = Query.CirclesTransfers(connection, query, true);
        return ResultWrapper<IEnumerable<CirclesTransferDto>>.Success(result);
    }

    public ResultWrapper<string> circles_computeTransfer(string from, string to, string amount)
    {
        // string result = LibPathfinder.ffi_compute_transfer(from, to, amount);
        return ResultWrapper<string>.Success("");
    }
}
