using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal readonly struct RszPrefabInfo
    {
        public const int Size = 8;

        public readonly uint StringOffset;
        public readonly uint ParentId;

        public RszPrefabInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for RszPrefabInfo.");

            StringOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            ParentId = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        }
    }
}
