using System.Buffers.Binary;
using Nethermind.Int256;

namespace Circles.Index.Pathfinder;

public class Balance
{
    public uint UserAddress { get; }
    public uint TokenAddress { get; }
    public UInt256 Value { get; }

    public Balance(uint userAddress, uint tokenAddress, UInt256 value)
    {
        UserAddress = userAddress;
        TokenAddress = tokenAddress;
        Value = value;
    }

    public void Serialize(Stream stream)
    {
        stream.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(UserAddress)));
        stream.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(TokenAddress)));

        byte[] bytes = Value.ToBigEndian();
        stream.WriteByte((byte)bytes.Length);
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            stream.WriteByte(bytes[i]);
        }
    }
}
