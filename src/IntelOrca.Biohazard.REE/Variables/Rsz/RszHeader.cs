using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    /// <summary>
    /// Represents the header of a RSZ file.
    /// </summary>
    
    internal class RszHeaderBase
    {

    }

    internal class RszHeader
    {
        public uint Magic;
        public uint Version;
        public uint ObjectCount;
        public uint InstanceCount;
        public uint UserdataCount;
        public uint Reserved;
        public ulong InstanceOffset;
        public ulong DataOffset;
        public ulong UserdataOffset;

        public int Size => Version < 4 ? 32 : 48;

        public RszHeader(ReadOnlySpan<byte> data)
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
