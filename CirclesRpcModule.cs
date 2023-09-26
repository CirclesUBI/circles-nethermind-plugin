using Nethermind.Api;
using Nethermind.Core;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Circles.Index;

public class CirclesRpcModule : ICirclesRpcModule
{
    private readonly INethermindApi _nethermindApi;
    private readonly SqlitePersistence _persistence;

    public CirclesRpcModule(INethermindApi nethermindApi)
    {
        IInitConfig initConfig = nethermindApi.Config<IInitConfig>();
        ILogger baseLogger = nethermindApi.LogManager.GetClassLogger();
        ILogger pluginLogger = new LoggerWithPrefix("Circles.Index.Rpc:", baseLogger);

        _persistence = new SqlitePersistence(Path.Combine(initConfig.BaseDbPath, Settings.DbFileName), 1, pluginLogger);
        _persistence.Initialize();

        _nethermindApi = nethermindApi ?? throw new ArgumentNullException(nameof(nethermindApi));
    }

    public ResultWrapper<Address[]> circles_getBalance(Address address)
    {
        Address[] tokens = _persistence.GetTokenAddressesForAccount(address);
        return ResultWrapper<Address[]>.Success(tokens);
    }
}
