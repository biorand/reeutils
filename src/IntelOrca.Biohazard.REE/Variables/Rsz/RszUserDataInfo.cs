using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal readonly struct RszUserDataInfo
    {
        public const int Size = 16;

        public readonly uint Hash;
        public readonly uint Crc;
        public readonly ulong StringOffset;

        public RszUserDataInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for RszUserDataInfo.");

            Hash = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            Crc = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            StringOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8, 8));
        }
    }
}
