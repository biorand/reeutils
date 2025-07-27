using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal readonly struct RszGameObject
    {
        public const int Size = 32;

        public readonly Guid Guid;
        public readonly int Id;
        public readonly int ParentId;
        public readonly ushort ComponentCount;
        public readonly int Ukn;
        public readonly short PrefabId;

        public RszGameObject(ReadOnlySpan<byte> data, bool isScn19 = false)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for RszGameObject.");

            // Convert ReadOnlySpan<byte> to byte[] for Guid constructor  
            byte[] guidBytes = data.Slice(0, 16).ToArray();
            Guid = new Guid(guidBytes);

            Id = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(16, 4));
            ParentId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(20, 4));
            ComponentCount = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(24, 2));

            // The order of Ukn and PrefabId depends on the version of the game
            if (isScn19)
            {
                PrefabId = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(26, 2));
                Ukn = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(28, 4));
            }
            else
            {
                Ukn = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(26, 4));
                PrefabId = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(30, 2));
            }
        }
    }
}
