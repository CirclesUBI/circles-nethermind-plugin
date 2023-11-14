# Circles Nethermind Plug-in
Indexes all Circles transactions to a SQLite database and provides a JSON-RPC module to query the data.

The plugin provides a JSON-RPC module to query the data. The following RPC methods are supported:

* `circles_getTotalBalance(Address address)`
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getTotalBalance",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545/
    ```

* `circles_getTokenBalances(Address address)`
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getTokenBalances",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545/
    ```

* `circles_getTrustRelations(Address address)`
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_getTrustRelations",
    "params":["0xde374ece6fa50e781e81aac78e811b33d16912c7"],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545/
    ```

* `circles_queryTrustEvents(CirclesTrustQuery query)`
    ```shell
  curl -X POST --data '{
      "jsonrpc":"2.0",
      "method":"circles_queryTrustEvents",
      "params":[{
        "UserAddress": "0xde374ece6fa50e781e81aac78e811b33d16912c7",
        "CanSendToAddress": "0xde374ece6fa50e781e81aac78e811b33d16912c7",
        "SortOrder": "Descending",
        "Mode": "Or",
        "Limit": 10
      }],
      "id":1
    }' -H "Content-Type: application/json" http://65.109.109.165:8545/
    ```

* `circles_queryHubTransfers(CirclesHubTransferQuery query)`
    ```shell
    curl -X POST --data '{
    "jsonrpc":"2.0",
    "method":"circles_queryHubTransfers",
    "params":[{
      "FromAddress": "0xde374ece6fa50e781e81aac78e811b33d16912c7",
      "ToAddress": "0xde374ece6fa50e781e81aac78e811b33d16912c7",
      "Mode": "Or",
      "Cursor": "27677614-1-2",
      "SortOrder": "Descending"
    }],
    "id":1
    }' -H "Content-Type: application/json" http://localhost:8545/
    ```

* `circles_queryCrcTransfers(CirclesTransferQuery query)`
    ```shell
    curl -X POST --data '{
      "jsonrpc":"2.0",
      "method":"circles_queryCrcTransfers",
      "params":[{
        "ToAddress": "0xde374ece6fa50e781e81aac78e811b33d16912c7",
        "FromAddress": "0x0000000000000000000000000000000000000000",
        "BlockNumberRange": {
          "Min": 29664296
        },
      "SortOrder": "Descending"
      }],
      "id":1
    }' -H "Content-Type: application/json" http://localhost:8545/
    ```

* `circles_computeTransfer(string query)`
    ```shell
  curl -X POST --data '{
    "jsonrpc": "2.0",
        "method": "circles_computeTransfer",
        "params": [
          "0xde374ece6fa50e781e81aac78e811b33d16912c7",
          "0xAEf690AD9Da7d54B880E3edC8E936518023086dD",
          "99999999999999999999999999999999999999",
        ],
        "id": 1
  }' -H "Content-Type: application/json" http://localhost:8545/
  ```

## Query Parameters

The query methods (like `circles_queryTrustEvents`) accept a parameter object with the following possible fields:

- `BlockNumberRange`: A range specifying the block numbers to filter by.
- `TransactionHash`: A specific transaction hash to filter by.
- `FromAddress`/`ToAddress`/`UserAddress`/`TokenAddress`: Addresses to filter the results by, depending on the specific query method.
- `Cursor`: An optional field used for pagination.
- `SortOrder`: Specifies the sorting order. Can be `Ascending` or `Descending`.

For the exact fields that each method accepts, refer to the provided code.
