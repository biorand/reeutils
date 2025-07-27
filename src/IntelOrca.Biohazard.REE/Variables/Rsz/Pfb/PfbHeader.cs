using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Pfb
{
    internal readonly struct PfbHeader
    {
        public const int Size = 56;

        public readonly uint Signature;
        public readonly uint InfoCount;
        public readonly uint ResourceCount;
        public readonly uint GameObjectRefInfoCount;
        public readonly uint UserdataCount;
        public readonly uint Reserved;
        public readonly ulong GameObjectRefInfoTbl;
        public readonly ulong ResourceInfoTbl;
        public readonly ulong UserdataInfoTbl;
        public readonly ulong DataOffset;

        public PfbHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for PfbHeader.");

            Signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            InfoCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            ResourceCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            GameObjectRefInfoCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
            UserdataCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
            Reserved = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
            GameObjectRefInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(24, 8));
            ResourceInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(32, 8));
            UserdataInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(40, 8));
            DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(48, 8));
        }
    }
}
