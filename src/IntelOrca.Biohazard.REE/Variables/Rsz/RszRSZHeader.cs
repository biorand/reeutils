using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal readonly struct RszRSZHeader
    {
        public readonly uint Magic;
        public readonly uint Version;
        public readonly uint ObjectCount;
        public readonly uint InstanceCount;
        public readonly uint UserdataCount;
        public readonly uint Reserved;
        public readonly ulong InstanceOffset;
        public readonly ulong DataOffset;
        public readonly ulong UserdataOffset;

        public int Size => Version < 4 ? 32 : 48;

        public RszRSZHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8)
                throw new ArgumentException("Insufficient data for RszRSZHeader.");

            Magic = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            Version = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));

            if (Version < 4)
            {
                ObjectCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
                InstanceCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
                UserdataCount = 0;
                Reserved = 0;
                InstanceOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(16, 8));
                DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(24, 8));
                UserdataOffset = 0;
            }
            else
            {
                ObjectCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
                InstanceCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
                UserdataCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
                Reserved = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
                InstanceOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(24, 8));
                DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(32, 8));
                UserdataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(40, 8));
            }
        }
    }
}
