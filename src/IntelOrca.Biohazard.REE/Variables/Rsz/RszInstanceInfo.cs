using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal readonly struct RszInstanceInfo
    {
        public const int Size = 8;

        public readonly uint TypeId;
        public readonly uint Crc;

        public RszInstanceInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for RszInstanceInfo.");

            TypeId = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            Crc = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        }
    }
}
