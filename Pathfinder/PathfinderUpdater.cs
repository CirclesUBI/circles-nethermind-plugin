using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using Circles.Index.Data.Cache;

namespace Circles.Index.Pathfinder;

public class PathfinderUpdater
{
    public static async Task<FileStream> ExportToBinaryFile(string outFilePath, string connectionString, MemoryCache cache)
    {
        string usersFilePath = Path.GetTempFileName();
        string orgsFilePath = Path.GetTempFileName();
        string trustsFilePath = Path.GetTempFileName();
        string balancesFilePath = Path.GetTempFileName();

        Console.WriteLine($"Reading users and orgs ..");
        Console.WriteLine($"Writing users ..");
        await using FileStream usersFile = File.Create(usersFilePath);
        KeyValuePair<string, uint>[] allSignupsOrdered = cache.SignupCache.AllUserIndexes.OrderBy(o => o.Value).ToArray();
        usersFile.Write(BitConverter.GetBytes((uint)BinaryPrimitives.ReverseEndianness(allSignupsOrdered.Length)));
        foreach (var (key, _) in allSignupsOrdered)
        {
            usersFile.Write(Convert.FromHexString(key));
        }

        Console.WriteLine($"Writing orgs ..");
        await using FileStream orgsFile = File.Create(orgsFilePath);
        KeyValuePair<string, uint>[] allOrgaIndexesOrdered = cache.SignupCache.OrganizationIndexes.OrderBy(o => o.Value).ToArray();
        orgsFile.Write(BitConverter.GetBytes((uint)BinaryPrimitives.ReverseEndianness(allOrgaIndexesOrdered.Length)));
        foreach ((string _, uint value) in allOrgaIndexesOrdered)
        {
            orgsFile.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
        }

        Console.WriteLine($"Reading trusts ..");
        await using FileStream trustsFile = File.Create(trustsFilePath);
        // using var t = new TrustReader(connectionString, Queries.TrustEdges, u.UserAddressIndexes);
        // var trustReader = await t.ReadTrustEdges();
        TrustEdge[] allTrustEdges = cache.Edges.ToArray();
        uint edgeCounter = 0;
        Console.WriteLine($"Writing trusts ..");
        trustsFile.Write(BitConverter.GetBytes((uint)BinaryPrimitives.ReverseEndianness(0)));
        foreach (TrustEdge trustEdge in allTrustEdges)
        {
            edgeCounter++;
            trustEdge.Serialize(trustsFile);
        }

        trustsFile.Position = 0;
        trustsFile.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(edgeCounter)));

        Console.WriteLine($"Reading balances ..");
        await using FileStream balancesFile = File.Create(balancesFilePath);

// TODO: Implement balance reader
        using BalanceReader b = new BalanceReader(connectionString, Queries.BalancesBySafeAndToken, cache.SignupCache.AllUserIndexes);
        IEnumerable<Balance> balanceReader = await b.ReadBalances();
        Console.WriteLine($"Writing balances ..");
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
        Console.WriteLine($"Writing output to {outFilePath} ..");

        usersFile.Position = 0;
        Console.WriteLine($"Writing users to offset {outFileStream.Position} ..");
        await usersFile.CopyToAsync(outFileStream);

        orgsFile.Position = 0;
        Console.WriteLine($"Writing orgs to offset {outFileStream.Position} ..");
        await orgsFile.CopyToAsync(outFileStream);

        trustsFile.Position = 0;
        Console.WriteLine($"Writing trusts to offset {outFileStream.Position} ..");
        await trustsFile.CopyToAsync(outFileStream);

        balancesFile.Position = 0;
        Console.WriteLine($"Writing balances to offset {outFileStream.Position} ..");
        await balancesFile.CopyToAsync(outFileStream);

        File.Delete(usersFilePath);
        File.Delete(orgsFilePath);
        File.Delete(trustsFilePath);
        File.Delete(balancesFilePath);

        outFileStream.Flush();
        outFileStream.Position = 0;

        return outFileStream;
    }

    static void ValidateData(FileStream fileStream)
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
        Console.WriteLine($"User section of file is from {0} to {userSectionEnd}");

        fileStream.Position = userSectionEnd;
        Debug.Assert(fileStream.Read(buffer) == 4);
        uint orgaCount = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buffer));
        uint orgaSectionEnd = userSectionEnd + 4 + (orgaCount * 4);
        Console.WriteLine($"Orga section of file is from {userSectionEnd} to {orgaSectionEnd}");

        fileStream.Position = orgaSectionEnd;
        Debug.Assert(fileStream.Read(buffer) == 4);
        uint trustCount = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buffer));
        const uint trustLength = 4 + 4 + 1;
        uint trustSectionEnd = orgaSectionEnd + 4 + (trustCount * trustLength);
        Console.WriteLine($"Trust section of file is from {orgaSectionEnd} to {trustSectionEnd}");

        fileStream.Position = trustSectionEnd;
        Debug.Assert(fileStream.Read(buffer) == 4);
        uint balanceCount = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(buffer));
        var readBalanceCount = 0;
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
            string balanceHolderAddress = Convert.ToHexString(balanceHolderAddressBuffer);
            fileStream.Position = pos;

            uint tokenOwner =
                BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(new ReadOnlySpan<byte>(headerBuffer, 4, 4)));
            pos = fileStream.Position;
            fileStream.Position = 4 + tokenOwner * 20;
            byte[] tokenOwnerAddressBuffer = new byte[20];
            Debug.Assert(fileStream.Read(tokenOwnerAddressBuffer) == tokenOwnerAddressBuffer.Length);
            string tokenOwnerAddress = Convert.ToHexString(tokenOwnerAddressBuffer);
            fileStream.Position = pos;

            byte balanceFieldLength = new ReadOnlySpan<byte>(headerBuffer, 8, 1)[0];
            byte[] balanceFieldBuffer = new byte[balanceFieldLength];
            Debug.Assert(fileStream.Read(balanceFieldBuffer) == balanceFieldBuffer.Length);

            BigInteger balance = new BigInteger(balanceFieldBuffer, true, true);
            Console.WriteLine($"{balanceHolderAddress};{tokenOwnerAddress};{balance}");

            balanceSectionEnd += (uint)(headerBuffer.Length + balanceFieldBuffer.Length);
            readBalanceCount++;
        }

        Debug.Assert(readBalanceCount == balanceCount);

        Console.WriteLine($"Balance section of file is from {trustSectionEnd} to {balanceSectionEnd}");
        Debug.Assert(balanceSectionEnd == fileStream.Length);

        Console.WriteLine(
            $"File length is {fileStream.Length}. Read bytes are: {balanceSectionEnd} File seems to be {(fileStream.Length == balanceSectionEnd ? "o.k." : "not o.k.")}");
        fileStream.Position = 0;
    }
}
