using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal readonly struct PfbHeader(int version, ReadOnlyMemory<byte> data)
    {
        public uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(0, 4));
        public uint GameObjectCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(4, 4));
        public uint ResourceCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(8, 4));
        public uint GameObjectRefCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(12, 4));
        public uint UserDataCount => Version < 17
            ? 0
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(16, 4));
        public ulong GameObjectRefOffset => Version < 17
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(16, 8))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(24, 8));
        public ulong ResourceOffset => Version < 17
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(24, 8))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(32, 8));
        public ulong UserDataOffset => Version < 17
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(32, 8))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(40, 8));
        public ulong DataOffset => Version < 17
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(40, 8))
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(48, 8));

        public int Version => version;
        public int Size => Version < 17 ? 48 : 56;
    }
}
