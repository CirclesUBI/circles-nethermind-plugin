using System.Buffers.Binary;

namespace Circles.Index.Pathfinder;

public class TrustEdge
{
    public uint UserAddress { get; }
    public uint CanSendToAddress { get; }
    public byte Limit { get; }

    public TrustEdge(uint userAddress, uint canSendToAddress, byte limit)
    {
        UserAddress = userAddress;
        CanSendToAddress = canSendToAddress;
        Limit = limit;
    }

    public void Serialize(Stream stream)
    {
        stream.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(UserAddress)));
        stream.Write(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(CanSendToAddress)));
        stream.WriteByte(BinaryPrimitives.ReverseEndianness(Limit));
    }
}
