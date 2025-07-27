using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal readonly struct RszResourceInfo
    {
        public const int Size = 8;

        public readonly uint StringOffset;
        public readonly uint Reserved;

        public RszResourceInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for RszResourceInfo.");

            StringOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            Reserved = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        }
    }
}
