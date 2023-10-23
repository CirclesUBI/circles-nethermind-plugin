using System.Buffers.Binary;
using System.Numerics;

namespace Circles.Index.Pathfinder;

public class Balance
{
    public uint UserAddress { get; }
    public uint TokenAddress { get; }
    public BigInteger Value { get; }

    public Balance(uint userAddress, uint tokenAddress, BigInteger value)
    {
        UserAddress = userAddress;
        TokenAddress = tokenAddress;
        Value = value;
    }

    public void Serialize(Stream stream)
    {
        stream.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(UserAddress)));
        stream.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(TokenAddress)));

        var bytes = Value.ToByteArray(true);
        stream.WriteByte((byte)bytes.Length);
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            stream.WriteByte(bytes[i]);
        }
    }
}
