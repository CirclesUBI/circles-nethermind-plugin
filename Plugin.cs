﻿using System.Threading.Channels;
using Circles.Index.Data.Cache;
using Circles.Index.Data.Sqlite;
using Circles.Index.Indexer;
using Circles.Index.Pathfinder;
using Circles.Index.Rpc;
using Circles.Index.Utils;
using Microsoft.Data.Sqlite;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Core;
using Nethermind.JsonRpc.Modules;
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

        // LibPathfinder.ffi_initialize();

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

            string indexDbLocation = Path.Combine(initConfig.BaseDbPath, settings.IndexDbFileName);
            string pathfinderDbLocation = Path.Combine(initConfig.BaseDbPath, settings.PathfinderDbFileName);
            SqliteConnection sinkConnection = new($"Data Source={indexDbLocation}");
            sinkConnection.Open();
            Sink sink = new(sinkConnection, 1000, pluginLogger);


            pluginLogger.Info("SQLite database at: " + indexDbLocation);
            pluginLogger.Info("Pathfinder database at: " + pathfinderDbLocation);

            _indexerContext = new StateMachine.Context(
                indexDbLocation,
                pathfinderDbLocation,
                _nethermindApi,
                pluginLogger,
                0, 0, 0,
                cache,
                sink,
                _cancellationTokenSource,
                settings);


            // Wait in a loop as long as the nethermind node is not fully in sync with the chain
            while (_indexerContext.NethermindApi.Pivot?.PivotNumber >
                   _indexerContext.NethermindApi.BlockTree?.Head?.Number
                   && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                pluginLogger.Info("Waiting for the node to sync");
                await Task.Delay(1000);
            }

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                return;
            }

            _indexerMachine = new StateMachine(_indexerContext);

            Channel<BlockEventArgs> blockChannel = Channel.CreateBounded<BlockEventArgs>(1);

            await Task.Run(async () =>
            {
                _indexerContext.NethermindApi.BlockTree!.NewHeadBlock += (_, args) =>
                {
                    blockChannel.Writer.TryWrite(args); // This will overwrite if the channel is full
                };

                // Process blocks from the channel
                _ = Task.Run(async () =>
                {
                    await foreach (BlockEventArgs args in blockChannel.Reader.ReadAllAsync(_cancellationTokenSource
                                       .Token))
                    {
                        try
                        {
                            if (args.Block.Number <= _indexerContext.LastIndexHeight 
                                && (_indexerContext.LastReorgAt == 0 || args.Block.Number <= _indexerContext.LastReorgAt))
                            {
                                _indexerContext.Logger.Warn($"Reorg at {args.Block.Number}");
                                _indexerContext.LastReorgAt = args.Block.Number;
                            }

                            _indexerContext.Logger.Debug($"New block received: {args.Block.Number}");
                            _indexerContext.CurrentChainHeight = args.Block.Number;

                            await _indexerMachine.HandleEvent(StateMachine.Event.NewBlock);
                        }
                        catch (Exception e)
                        {
                            _indexerContext.Logger.Error("Error while indexing new block", e);
                        }
                    }
                }, _cancellationTokenSource.Token);

                await _indexerMachine.TransitionTo(StateMachine.State.Initial);
            }, _cancellationTokenSource.Token);
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

        (IApiWithNetwork apiWithNetwork, _) = _nethermindApi.ForRpc;
        CirclesRpcModule circlesRpcModule =
            new(_nethermindApi, _indexerContext.MemoryCache, _indexerContext.IndexDbLocation);
        apiWithNetwork.RpcModuleProvider?.Register(new SingletonModulePool<ICirclesRpcModule>(circlesRpcModule));
    }

    public ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        return ValueTask.CompletedTask;
    }
}