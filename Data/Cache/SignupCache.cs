using System.Collections.Concurrent;

namespace Circles.Index.Data.Cache;

public class SignupCache
{
    private readonly ConcurrentDictionary<string, uint> _userAddressIndexes = new();
    private readonly ConcurrentDictionary<string, uint> _organizationAddressIndexes = new();
    private readonly ConcurrentDictionary<string, string> _personToTokenMap = new();
    private readonly ConcurrentDictionary<string, string> _tokenToPersonMap = new();
    private readonly ConcurrentDictionary<string, object?> _organizationAddresses = new();

    public long OrganizationCount => _organizationAddresses.Count;
    public long PersonCount => _personToTokenMap.Count;
    public long UserCount => _userAddressIndexes.Count;

    public IDictionary<string,uint> AllUserIndexes => _userAddressIndexes;
    public IDictionary<string,uint> OrganizationIndexes => _organizationAddressIndexes;

    public bool IsOrganization(string address) => _organizationAddresses.ContainsKey(address);

    public bool IsPerson(string address) => _personToTokenMap.ContainsKey(address);

    public bool IsUser(string address) => IsPerson(address) || IsOrganization(address);

    public bool IsCirclesToken(string address) => _tokenToPersonMap.ContainsKey(address);

    public string? FindCirclesToken(string address) =>
        _personToTokenMap.TryGetValue(address, out string? tokenAddress)
            ? tokenAddress
            : null;

    public void Add(string signupAddress, string? tokenAddress)
    {
        _userAddressIndexes.TryAdd(signupAddress, (uint)_userAddressIndexes.Count);

        if (tokenAddress is null)
        {
            _organizationAddressIndexes.TryAdd(signupAddress, (uint)_userAddressIndexes.Count);
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
