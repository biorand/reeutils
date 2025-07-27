using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal readonly struct RszRSZUserDataInfo
    {
        public const int Size = 16;

        public readonly uint InstanceId;
        public readonly uint Hash;
        public readonly ulong StringOffset;

        public RszRSZUserDataInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for RszRSZUserDataInfo.");

            InstanceId = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            Hash = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            StringOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8, 8));
        }
    }
}
