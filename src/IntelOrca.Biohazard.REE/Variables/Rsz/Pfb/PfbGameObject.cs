using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Pfb
{
    internal readonly struct PfbGameObject
    {
        public const int Size = 12;

        public readonly int Id;
        public readonly int ParentId;
        public readonly int ComponentCount;

        public PfbGameObject(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for PfbGameObject.");

            Id = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
            ParentId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
            ComponentCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
        }
    }
}
