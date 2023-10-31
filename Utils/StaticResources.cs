using System.Collections.Immutable;
using System.Reflection;
using Nethermind.Core.Crypto;

namespace Circles.Index.Utils;

public static class StaticResources
{
    public const string AddressEmptyBytesPrefix = "0x000000000000000000000000";

    public static Hash256 CrcHubTransferEventTopic { get; } =
        new("0x8451019aab65b4193860ef723cb0d56b475a26a72b7bfc55c1dbd6121015285a");

    public static Hash256 CrcTrustEventTopic { get; } =
        new("0xe60c754dd8ab0b1b5fccba257d6ebcd7d09e360ab7dd7a6e58198ca1f57cdcec");

    public static Hash256 CrcSignupEventTopic { get; } =
        new("0x358ba8f768af134eb5af120e9a61dc1ef29b29f597f047b555fc3675064a0342");

    public static Hash256 CrcOrganisationSignupEventTopic { get; } =
        new("0xb0b94cff8b84fc67513b977d68a5cdd67550bd9b8d99a34b570e3367b7843786");

    public static Hash256 Erc20TransferTopic { get; } =
        new("0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef");

    public static (ImmutableHashSet<long> KnownBlocks, long MaxKnownBlock, long MinKnownBlock) GetKnownRelevantBlocks(ulong chainId)
    {
        Assembly assembly = Assembly.GetAssembly(typeof(StaticResources))!;
        string fullResourceName = $"{assembly.GetName().Name}.cheatcodes.{chainId}.known_relevant_blocks.csv";
        Stream? resourceStream = assembly.GetManifestResourceStream(fullResourceName);
        if (resourceStream == null)
        {
            return (ImmutableHashSet<long>.Empty, -1, -1);
        }

        long maxKnownBlock = -1;

        using (resourceStream)
        using (StreamReader streamReader = new(resourceStream))
        {
            HashSet<long> knownRelevantBlocks = new();
            do
            {
                string? line = streamReader.ReadLine();
                if (line == null)
                {
                    break;
                }

                if (!long.TryParse(line, out long blockNumber))
                {
                    throw new Exception($"Could not parse block number in line {line} from resource {fullResourceName}.");
                }

                knownRelevantBlocks.Add(blockNumber);
                if (blockNumber > maxKnownBlock)
                {
                    maxKnownBlock = blockNumber;
                }
            } while (true);

            return (knownRelevantBlocks.ToImmutableHashSet(), maxKnownBlock, knownRelevantBlocks.First());
        }
    }
}
