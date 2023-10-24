using System.Collections.Concurrent;
using Circles.Index.Rpc;
using Nethermind.Core;

namespace Circles.Index.Data.Cache;

public class TrustGraph
{
    internal readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _trusts = new(Environment.ProcessorCount, Settings.InitialUserCacheSize);
    internal readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _trustedBy = new(Environment.ProcessorCount, Settings.InitialUserCacheSize);

    public void AddOrUpdateEdge(string userAddress, string canSendToAddress, int limit)
    {
        ConcurrentDictionary<string, int> trusts =
            _trusts.GetOrAdd(canSendToAddress, _ => new ConcurrentDictionary<string, int>());
        if (limit == 0)
        {
            trusts.TryRemove(userAddress, out _);
        }
        else
        {
            trusts[userAddress] = limit;
        }

        ConcurrentDictionary<string, int> trustedBy =
            _trustedBy.GetOrAdd(userAddress, _ => new ConcurrentDictionary<string, int>());
        if (limit == 0)
        {
            trustedBy.TryRemove(canSendToAddress, out _);
        }
        else
        {
            trustedBy[canSendToAddress] = limit;
        }
    }

    public void RemoveUser(string signupCirclesAddress)
    {
        if (_trusts.TryRemove(signupCirclesAddress, out ConcurrentDictionary<string, int>? trusts))
        {
            foreach (string canSendToAddress in trusts.Keys)
            {
                _trustedBy[canSendToAddress].TryRemove(signupCirclesAddress, out _);
            }
        }

        if (_trustedBy.TryRemove(signupCirclesAddress, out ConcurrentDictionary<string, int>? trustedBy))
        {
            foreach (string userAddress in trustedBy.Keys)
            {
                _trusts[userAddress].TryRemove(signupCirclesAddress, out _);
            }
        }
    }

    public bool TryGet(string address, out TrustRelations? trustRelations)
    {
        if (_trusts.TryGetValue(address, out ConcurrentDictionary<string, int>? trusts) &&
            _trustedBy.TryGetValue(address, out ConcurrentDictionary<string, int>? trustedBy))
        {
            trustRelations = new TrustRelations(new Address(address), trusts, trustedBy);
            return true;
        }

        trustRelations = default;
        return false;
    }
}
