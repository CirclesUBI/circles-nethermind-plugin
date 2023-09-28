# Circles Nethermind Plug-in
Indexes all Circles transactions to a SQLite database and provides a JSON-RPC module to query the data.

## Prerequisites
* .Net 7 SDK
* Nethermind configured to use Gnosis Chain (ID 100/0x64)

## Build
```shell
dotnet add package Microsoft.Data.Sqlite
dotnet build
```

## Use with Nethermind
After building the plugin, copy the `Circles.Index.dll` file to the `Plugins` folder of your Nethermind installation. Then add the following to your config to enable the Circles RPC module:
```json
{
    "JsonRpc": {
        "Enabled": true,
        "EnginePort": 8551,
        "EnabledModules": ["Circles", ... your existing modules ...]
    }
}
```

## Rpc methods
The following RPC methods are supported:
* circles_getTotalBalance(Address address)
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getTotalBalance",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
* circles_getTokenBalances(Address address)
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getTokenBalances",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
* circles_getTrustRelations(Address address)
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getTrustRelations",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
* circles_getHubTransfers(Address address)
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getHubTransfers",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
* circles_getCrcTransfers(Address address)
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getCrcTransfers",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
* circles_bulkGetTrustRelations()
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_bulkGetTrustRelations",
    "params":[],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
* circles_bulkGetUsers()
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_bulkGetUsers",
    "params":[],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
* circles_bulkGetOrganizations()
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_bulkGetOrganizations",
    "params":[],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545
    ```
