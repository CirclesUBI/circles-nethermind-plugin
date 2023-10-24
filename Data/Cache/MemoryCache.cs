using System.Collections.Concurrent;
using Circles.Index.Data.Model;
using Circles.Index.Pathfinder;

namespace Circles.Index.Data.Cache;

public class MemoryCache
{
    public SignupCache SignupCache { get; } = new();
    public TrustGraph TrustGraph { get; } = new();

    public BalanceCache Balances { get; } = new();

    public IEnumerable<TrustEdge> Edges
    {
        get
        {
            IDictionary<string, uint> userIndexes = SignupCache.AllUserIndexes;
            foreach (KeyValuePair<string,ConcurrentDictionary<string,int>> trust in TrustGraph._trusts)
            {
                foreach (KeyValuePair<string,int> pair in trust.Value)
                {
                    if (!userIndexes.TryGetValue(trust.Key, out uint userIndex)
                        || !userIndexes.TryGetValue(pair.Key, out uint canSendToIndex))
                    {
                        // This is a trust relation to a user that didn't sign up yet
                        continue;
                    }

                    yield return new TrustEdge(userIndex, canSendToIndex, (byte)pair.Value);
                }
            }
        }
    }

    public void RemoveUser(CirclesSignupDto signup)
    {
        if (signup.TokenAddress is null)
        {
            SignupCache.RemoveOrganization(signup.CirclesAddress);
        }
        else
        {
            SignupCache.RemovePerson(signup.CirclesAddress, signup.TokenAddress);
        }

        TrustGraph.RemoveUser(signup.CirclesAddress);
    }

    public void RemoveTrustRelation(CirclesTrustDto trust)
    {
        TrustGraph.AddOrUpdateEdge(trust.UserAddress, trust.CanSendToAddress, 0);
    }

    public bool IsCirclesToken(string logLoggersAddress)
        => SignupCache.IsCirclesToken(logLoggersAddress);
}
