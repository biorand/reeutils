using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Scn
{
    internal readonly struct ScnHeader
    {
        public const int Size = 64;

        public readonly uint Signature;
        public readonly uint InfoCount;
        public readonly uint ResourceCount;
        public readonly uint FolderCount;
        public readonly uint PrefabCount;
        public readonly uint UserdataCount;
        public readonly ulong FolderTbl;
        public readonly ulong ResourceInfoTbl;
        public readonly ulong PrefabInfoTbl;
        public readonly ulong UserdataInfoTbl;
        public readonly ulong DataOffset;

        public ScnHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for ScnHeader.");

            Signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            InfoCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            ResourceCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            FolderCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
            PrefabCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
            UserdataCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
            FolderTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(24, 8));
            ResourceInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(32, 8));
            PrefabInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(40, 8));
            UserdataInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(48, 8));
            DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(56, 8));
        }
    }
}
