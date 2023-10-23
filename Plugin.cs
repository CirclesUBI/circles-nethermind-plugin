using Circles.Index.Data.Cache;
using Circles.Index.Data.Sqlite;
using Circles.Index.Indexer;
using Circles.Index.Rpc;
using Circles.Index.Utils;
using Microsoft.Data.Sqlite;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.Logging;

namespace Circles.Index;

public class CirclesIndex : INethermindPlugin
{
    public string Name => "Circles.Index";

    public string Description =>
        "Indexes Circles related events and provides query capabilities via JSON-RPC. Indexed events are: Signup, OrgSignup, Trust, HubTransfer, CrcTransfer.";

    public string Author => "Gnosis";

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private INethermindApi? _nethermindApi;

    private StateMachine? _indexerMachine;
    private StateMachine.Context? _indexerContext;

    public Task Init(INethermindApi nethermindApi)
    {
        _nethermindApi = nethermindApi;
        Run();

        return Task.CompletedTask;
    }

    private async void Run()
    {
        try
        {
            if (_nethermindApi == null)
            {
                throw new Exception("_nethermindApi is not set");
            }

            ILogger baseLogger = _nethermindApi.LogManager.GetClassLogger();
            ILogger pluginLogger = new LoggerWithPrefix("Circles.Index:", baseLogger);
            IInitConfig initConfig = _nethermindApi.Config<IInitConfig>();

            Settings settings = new();
            MemoryCache cache = new();

            string dbLocation = Path.Combine(initConfig.BaseDbPath, settings.DbFileName);
            SqliteConnection sinkConnection = new($"Data Source={dbLocation}");
            sinkConnection.Open();
            Sink sink = new(sinkConnection, 1000, pluginLogger);

            _indexerContext = new StateMachine.Context(
                dbLocation,
                _nethermindApi,
                pluginLogger,
                0, 0, 0,
                cache,
                sink,
                _cancellationTokenSource,
                settings);

            _indexerMachine = new StateMachine(_indexerContext);


            // TODO: Handle the case that the node is not synced yet
            if (_indexerContext.NethermindApi.BlockTree == null)
            {
                throw new Exception("BlockTree is null");
            }

            await Task.Run(async () =>
            {
                _indexerContext.NethermindApi.BlockTree.NewHeadBlock += async (_, args) =>
                {
                    try
                    {
                        if (args.Block.Number <= _indexerContext.LastIndexHeight)
                        {
                            _indexerContext.Logger.Info(
                                $"Ignoring block {args.Block.Number} because it was already indexed");
                            return;
                        }

                        _indexerContext.Logger.Info($"New block received: {args.Block.Number}");
                        _indexerContext.CurrentChainHeight = args.Block.Number;
                        await _indexerMachine.HandleEvent(StateMachine.Event.NewBlock);
                    }
                    catch (Exception e)
                    {
                        _indexerContext.Logger.Error("Error while indexing new block", e);
                    }
                };

                await _indexerMachine.TransitionTo(StateMachine.State.Initial);
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }


    public Task InitNetworkProtocol()
    {
        return Task.CompletedTask;
    }


    public async Task InitRpcModules()
    {
        if (_nethermindApi == null)
        {
            throw new Exception("_nethermindApi is not set");
        }

        if (_nethermindApi.RpcModuleProvider == null)
        {
            throw new Exception("_nethermindApi.RpcModuleProvider is not set");
        }

        if (_indexerContext == null)
        {
            throw new Exception("_indexerContext is not set");
        }

        (IApiWithNetwork apiWithNetwork, _) =  _nethermindApi.ForRpc;
        IRpcModule rpcModule = await _nethermindApi.RpcModuleProvider.Rent("eth_call", false);
        IEthRpcModule ethRpcModule = rpcModule as IEthRpcModule ?? throw new Exception("eth_call module is not IEthRpcModule");
        CirclesRpcModule circlesRpcModule = new(_nethermindApi, ethRpcModule, _indexerContext.MemoryCache, _indexerContext.DbLocation);
        apiWithNetwork.RpcModuleProvider?.Register(new SingletonModulePool<ICirclesRpcModule>(circlesRpcModule));
    }

    public ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        return ValueTask.CompletedTask;
    }
}
