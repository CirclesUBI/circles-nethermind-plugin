using Circles.Index.Data.Cache;
using Circles.Index.Data.Model;
using Circles.Index.Data.Sqlite;
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
    private readonly IEthRpcModule? _ethRpcModule;
    private readonly ILogger _pluginLogger;

    private readonly string _dbLocation;
    private readonly MemoryCache _cache;

    public CirclesRpcModule(INethermindApi nethermindApi, IEthRpcModule ethRpcModule, MemoryCache cache, string dbLocation)
    {
        ILogger baseLogger = nethermindApi.LogManager.GetClassLogger();
        _pluginLogger = new LoggerWithPrefix("Circles.Index.Rpc:", baseLogger);

        _ethRpcModule = ethRpcModule;
        _dbLocation = dbLocation;
        _cache = cache;
    }

    public ResultWrapper<UInt256> circles_getTotalBalance(Address address)
    {
        if (_ethRpcModule == null)
        {
            return ResultWrapper<UInt256>.Success(new UInt256(0));
        }

        using SqliteConnection connection = new($"Data Source={_dbLocation}");
        connection.Open();
        _pluginLogger.Info("circles_getTotalBalance: Query connection opened");
        connection.Disposed += (sender, args) => _pluginLogger.Info("circles_getTotalBalance: Query connection disposed");

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

        using SqliteConnection connection = new($"Data Source={_dbLocation}");
        connection.Open();
        _pluginLogger.Info("circles_getTokenBalances: Query connection opened");
        connection.Disposed += (sender, args) => Console.WriteLine("circles_getTokenBalances: Query connection disposed");
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
        return !_cache.TrustGraph.TryGet(address.ToString(true, false), out TrustRelations? trustRelations)
            ? ResultWrapper<TrustRelations>.Fail("Couldn't get the trust relations")
            : ResultWrapper<TrustRelations>.Success(trustRelations!);
    }

    public ResultWrapper<IEnumerable<CirclesHubTransferDto>> circles_getHubTransfers(Address address)
    {
        // On purpose not in a 'using' block, because the connection is used in the Query.CirclesHubTransfers() method:
        SqliteConnection connection = new($"Data Source={_dbLocation}");
        _pluginLogger.Info("circles_getHubTransfers: Query connection opened");
        connection.Disposed += (sender, args) => Console.WriteLine("circles_getHubTransfers: Query connection disposed");
        connection.Open();

        IEnumerable<CirclesHubTransferDto> hubTransfers = Query.CirclesHubTransfers(
            connection,
            new CirclesHubTransferQuery
            {
                FromAddress = address.ToString(true, false),
                ToAddress = address.ToString(true, false),
                SortOrder = SortOrder.Descending,
                Mode = QueryMode.Or
            },
            int.MaxValue, true);

        return ResultWrapper<IEnumerable<CirclesHubTransferDto>>.Success(hubTransfers);
    }

    public ResultWrapper<IEnumerable<CirclesTransferDto>> circles_getCrcTransfers(Address address)
    {
        // On purpose not in a 'using' block, because the connection is used in the Query.CirclesTransfers() method:
        SqliteConnection connection = new($"Data Source={_dbLocation}");
        connection.Open();
        _pluginLogger.Info("circles_getCrcTransfers: Query connection opened");
        connection.Disposed += (sender, args) => Console.WriteLine("circles_getCrcTransfers: Query connection disposed");

        IEnumerable<CirclesTransferDto> crcTransfer = Query.CirclesTransfers(
            connection,
            new CirclesTransferQuery
            {
                FromAddress = address.ToString(true, false),
                ToAddress = address.ToString(true, false),
                SortOrder = SortOrder.Descending,
                Mode = QueryMode.Or
            },
            int.MaxValue, true);

        return ResultWrapper<IEnumerable<CirclesTransferDto>>.Success(crcTransfer);
    }
}
