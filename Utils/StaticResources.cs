using System.Collections.Immutable;
using System.Reflection;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Circles.Index.Utils;

public static class StaticResources
{
    public const int AddressEmptyBytesPrefixLength = 26;

    public static Address EUReTokenAddress { get; } = new("0xcB444e90D8198415266c6a2724b7900fb12FC56E");
    public static Address GBPeTokenAddress { get; } = new("0x5Cb9073902F2035222B9749F8fB0c9BFe5527108");
    public static Address ISKeTokenAddress { get; } = new("0xD8F84BF2E036A3c8E4c0e25ed2aAe0370F3CCca8");
    public static Address USDeTokenAddress { get; } = new("0x20E694659536C6B46e4B8BE8f6303fFCD8d1dF69");
    public static Address TetherTokenAddress { get; } = new("0x4ecaba5870353805a9f068101a40e0f32ed605c6");
    public static Address USDcTokenAddress { get; } = new("0xddafbb505ad214d7b80b1f830fccc89b60fb7a83");
    public static Address CurveFiUSD = new("0xabef652195f98a91e490f047a5006b71c85f058d");
    public static Address GNOTokenAddress { get; } = new("0x9C58BAcC331c9aa871AFD802DB6379a98e80CEdb");

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
}