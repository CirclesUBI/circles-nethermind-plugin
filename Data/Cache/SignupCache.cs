using System.Collections.Concurrent;

namespace Circles.Index.Data.Cache;

public class SignupCache
{
    private readonly ConcurrentDictionary<string, uint> _userAddressIndexes = new(Environment.ProcessorCount, Settings.InitialUserCacheSize);
    private readonly ConcurrentDictionary<string, uint> _organizationAddressIndexes = new(Environment.ProcessorCount, Settings.InitialOrgCacheSize);
    private readonly ConcurrentDictionary<string, string> _personToTokenMap = new(Environment.ProcessorCount, Settings.InitialUserCacheSize);
    private readonly ConcurrentDictionary<string, string> _tokenToPersonMap = new(Environment.ProcessorCount, Settings.InitialUserCacheSize);
    private readonly ConcurrentDictionary<string, object?> _organizationAddresses = new(Environment.ProcessorCount, Settings.InitialOrgCacheSize);

    public IDictionary<string,uint> AllUserIndexes => _userAddressIndexes;
    public IDictionary<string,uint> OrganizationIndexes => _organizationAddressIndexes;

    public bool IsCirclesToken(string address) => _tokenToPersonMap.ContainsKey(address);

    public string GetTokenOwner(string tokenAddress) => _tokenToPersonMap[tokenAddress];

    public void Add(string signupAddress, string? tokenAddress)
    {
        uint index = (uint)_userAddressIndexes.Count;
        _userAddressIndexes.TryAdd(signupAddress, index);

        if (tokenAddress is null)
        {
            _organizationAddressIndexes.TryAdd(signupAddress, index);
            _organizationAddresses.TryAdd(signupAddress, null);
        }
        else
        {
            _personToTokenMap.TryAdd(signupAddress, tokenAddress);
            _tokenToPersonMap.TryAdd(tokenAddress, signupAddress);
        }
    }

    public void RemoveOrganization(string circlesAddress)
    {
        _organizationAddresses.TryRemove(circlesAddress, out object? _);
    }

    public void RemovePerson(string circlesAddress, string signupTokenAddress)
    {
        _personToTokenMap.TryRemove(circlesAddress, out string? _);
        _tokenToPersonMap.TryRemove(signupTokenAddress, out string? _);
    }
}
