using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables
{
    internal readonly struct GameObjectRefInfo
    {
        public const int Size = 16;

        public readonly int ObjectId;
        public readonly int PropertyId;
        public readonly int ArrayIndex;
        public readonly int TargetId;

        public GameObjectRefInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for GameObjectRefInfo.");

            ObjectId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
            PropertyId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
            ArrayIndex = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
            TargetId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12, 4));
        }
    }
}
