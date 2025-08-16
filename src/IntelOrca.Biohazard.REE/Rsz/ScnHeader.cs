using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal readonly struct ScnHeader(int version, ReadOnlyMemory<byte> data)
    {
        public uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(0, 4));
        public uint InfoCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(4, 4));
        public uint ResourceCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(8, 4));
        public uint FolderCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(12, 4));
        public uint UserDataCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(16, 4));
        public uint PrefabCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(20, 4));
        public ulong FolderOffset => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(24, 8));
        public ulong ResourceOffset => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(32, 8));
        public ulong PrefabOffset => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(40, 8));
        public ulong UserdataOffset => Version <= 18
            ? 0
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(48, 8));

        public ulong DataOffset => Version <= 18
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(48, 8))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(56, 8));

        public int Version => version;
        public int Size => Version >= 19 ? 64 : 56;
    }
}
