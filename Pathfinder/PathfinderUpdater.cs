using System.Buffers.Binary;
using Circles.Index.Data.Cache;

namespace Circles.Index.Pathfinder;

public static class PathfinderUpdater
{
    public static async Task<FileStream> ExportToBinaryFile(string outFilePath, MemoryCache cache)
    {
        string usersFilePath = Path.GetTempFileName();
        string orgsFilePath = Path.GetTempFileName();
        string trustsFilePath = Path.GetTempFileName();
        string balancesFilePath = Path.GetTempFileName();

        await using FileStream usersFile = File.Create(usersFilePath);
        KeyValuePair<string, uint>[] allSignupsOrdered =
            cache.SignupCache.AllUserIndexes.OrderBy(o => o.Value).ToArray();

        usersFile.Write(BitConverter.GetBytes((uint)BinaryPrimitives.ReverseEndianness(allSignupsOrdered.Length)));
        foreach ((string key, uint _) in allSignupsOrdered)
        {
            usersFile.Write(Convert.FromHexString(key.Substring(2)));
        }

        await using FileStream orgsFile = File.Create(orgsFilePath);
        KeyValuePair<string, uint>[] allOrgaIndexesOrdered =
            cache.SignupCache.OrganizationIndexes.OrderBy(o => o.Value).ToArray();
        orgsFile.Write(BitConverter.GetBytes((uint)BinaryPrimitives.ReverseEndianness(allOrgaIndexesOrdered.Length)));
        foreach ((string _, uint value) in allOrgaIndexesOrdered)
        {
            orgsFile.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
        }

        await using FileStream trustsFile = File.Create(trustsFilePath);
        TrustEdge[] allTrustEdges = cache.Edges.ToArray();
        uint edgeCounter = 0;
        trustsFile.Write(BitConverter.GetBytes((uint)BinaryPrimitives.ReverseEndianness(0)));
        foreach (TrustEdge trustEdge in allTrustEdges)
        {
            edgeCounter++;
            trustEdge.Serialize(trustsFile);
        }

        trustsFile.Position = 0;
        trustsFile.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(edgeCounter)));

        await using FileStream balancesFile = File.Create(balancesFilePath);

        IEnumerable<Balance> balanceReader = cache.Balances._balancesPerAccountAndToken
            .SelectMany(o =>
                o.Value.Select(p =>
                {
                    string tokenOwner = cache.SignupCache.GetTokenOwner(p.Key);
                    uint tokenOwnerIndex = cache.SignupCache.AllUserIndexes[tokenOwner];

                    return !cache.SignupCache.AllUserIndexes.TryGetValue(o.Key, out uint balanceHolder)
                        ? // CRC can be transferred to non-circles users but we can't consider them in the pathfinder
                        null
                        : new Balance(balanceHolder, tokenOwnerIndex, p.Value);
                })
            )
            .Where(o => o != null)
            .Select(o => o!);

        uint balanceCounter = 0;
        balancesFile.Write(BitConverter.GetBytes((uint)BinaryPrimitives.ReverseEndianness(0)));
        foreach (Balance balance in balanceReader)
        {
            balanceCounter++;
            balance.Serialize(balancesFile);
        }

        balancesFile.Position = 0;
        balancesFile.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(balanceCounter)));

        FileStream outFileStream = File.Create(outFilePath);

        usersFile.Position = 0;
        await usersFile.CopyToAsync(outFileStream);

        orgsFile.Position = 0;
        await orgsFile.CopyToAsync(outFileStream);

        trustsFile.Position = 0;
        await trustsFile.CopyToAsync(outFileStream);

        balancesFile.Position = 0;
        await balancesFile.CopyToAsync(outFileStream);

        File.Delete(usersFilePath);
        File.Delete(orgsFilePath);
        File.Delete(trustsFilePath);
        File.Delete(balancesFilePath);

        outFileStream.Flush();
        outFileStream.Position = 0;

        return outFileStream;
    }
}
