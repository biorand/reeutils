using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables
{
    internal readonly struct UsrHeader
    {
        public const int Size = 48;

        public readonly uint Signature;
        public readonly uint ResourceCount;
        public readonly uint UserdataCount;
        public readonly uint InfoCount;
        public readonly ulong ResourceInfoTbl;
        public readonly ulong UserdataInfoTbl;
        public readonly ulong DataOffset;
        public readonly ulong Reserved;

        public UsrHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for UsrHeader.");

            Signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            ResourceCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            UserdataCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            InfoCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
            ResourceInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(16, 8));
            UserdataInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(24, 8));
            DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(32, 8));
            Reserved = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(40, 8));
        }
    }
}
