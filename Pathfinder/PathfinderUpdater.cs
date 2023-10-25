using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
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

    public static void ValidateData(FileStream fileStream)
    {
        /*
        {
           addressCount: uint32
           addresses: byte[20][]
           organizationsCount: uint32
           organizations: uint32[]
           trustEdgesCount: uint32
           trustEdges: {
              userAddress: uint32
              canSendToAddress: uint32
              limit: byte
           }[]
           balancesCount: uint32
           balances: {
              userAddress: uint32
              tokenOwnerAddress: uint32
              balance: uint256
           }[]
        }
         */
        byte[] buffer = new byte[4];
        Debug.Assert(fileStream.Read(buffer) == 4);
        uint userCount = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buffer));
        const uint addressLength = 20;
        uint userSectionEnd = 4 + (userCount * addressLength);

        fileStream.Position = userSectionEnd;
        Debug.Assert(fileStream.Read(buffer) == 4);
        uint orgaCount = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buffer));
        uint orgaSectionEnd = userSectionEnd + 4 + (orgaCount * 4);

        fileStream.Position = orgaSectionEnd;
        Debug.Assert(fileStream.Read(buffer) == 4);
        uint trustCount = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buffer));
        const uint trustLength = 4 + 4 + 1;
        uint trustSectionEnd = orgaSectionEnd + 4 + (trustCount * trustLength);

        fileStream.Position = trustSectionEnd;
        Debug.Assert(fileStream.Read(buffer) == 4);
        uint balanceCount = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buffer));
        int readBalanceCount = 0;
        uint balanceSectionEnd = trustSectionEnd + 4;

        byte[] headerBuffer = new byte[9];
        while (true)
        {
            if (fileStream.Read(headerBuffer) != headerBuffer.Length)
            {
                break;
            }

            uint balanceHolder =
                BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(new ReadOnlySpan<byte>(headerBuffer, 0, 4)));
            long pos = fileStream.Position;
            fileStream.Position = 4 + balanceHolder * 20;
            byte[] balanceHolderAddressBuffer = new byte[20];
            Debug.Assert(fileStream.Read(balanceHolderAddressBuffer) == balanceHolderAddressBuffer.Length);
            fileStream.Position = pos;

            uint tokenOwner =
                BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(new ReadOnlySpan<byte>(headerBuffer, 4, 4)));
            pos = fileStream.Position;
            fileStream.Position = 4 + tokenOwner * 20;
            byte[] tokenOwnerAddressBuffer = new byte[20];
            Debug.Assert(fileStream.Read(tokenOwnerAddressBuffer) == tokenOwnerAddressBuffer.Length);
            fileStream.Position = pos;

            byte balanceFieldLength = new ReadOnlySpan<byte>(headerBuffer, 8, 1)[0];
            byte[] balanceFieldBuffer = new byte[balanceFieldLength];
            Debug.Assert(fileStream.Read(balanceFieldBuffer) == balanceFieldBuffer.Length);

            balanceSectionEnd += (uint)(headerBuffer.Length + balanceFieldBuffer.Length);
            readBalanceCount++;
        }

        StringBuilder summaryBuilder = new();

        // Users section summary
        summaryBuilder.AppendLine($"Users Section:");
        summaryBuilder.AppendLine($"\tUser Count: {userCount}");
        summaryBuilder.AppendLine($"\tTotal Bytes: {userSectionEnd - 4}");

        // Organizations section summary
        summaryBuilder.AppendLine($"Organizations Section:");
        summaryBuilder.AppendLine($"\tOrganization Count: {orgaCount}");
        summaryBuilder.AppendLine($"\tTotal Bytes: {orgaSectionEnd - userSectionEnd}");

        // Trust edges section summary
        summaryBuilder.AppendLine($"Trust Edges Section:");
        summaryBuilder.AppendLine($"\tTrust Edge Count: {trustCount}");
        summaryBuilder.AppendLine($"\tTotal Bytes: {trustSectionEnd - orgaSectionEnd}");

        // Balances section summary
        summaryBuilder.AppendLine($"Balances Section:");
        summaryBuilder.AppendLine($"\tBalance Entry Count: {readBalanceCount}");
        summaryBuilder.AppendLine($"\tTotal Bytes: {balanceSectionEnd - trustSectionEnd}");

        // Overall file summary
        summaryBuilder.AppendLine($"Overall File:");
        summaryBuilder.AppendLine($"\tTotal Bytes: {fileStream.Length}");

        Console.WriteLine(summaryBuilder.ToString());

        Debug.Assert(readBalanceCount == balanceCount);
        Debug.Assert(balanceSectionEnd == fileStream.Length);

        fileStream.Position = 0;
    }
}
