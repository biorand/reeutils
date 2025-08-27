using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal readonly struct RszHeader(ReadOnlyMemory<byte> data)
    {
        public uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(0, 4));
        public uint Version => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(4, 4));
        public uint ObjectCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(8, 4));
        public uint InstanceCount => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(12, 4));
        public uint UserDataCount => Version < 4
            ? 0
            : BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(16, 4));
        public ulong InstanceOffset => Version < 4
            ? BinaryPrimitives.ReadUInt64LittleEndian(data.Span.Slice(16, 8))
            : BinaryPrimitives.ReadUInt64LittleEndian(data.Span.Slice(24, 8));
        public ulong DataOffset => Version < 4
            ? BinaryPrimitives.ReadUInt64LittleEndian(data.Span.Slice(24, 8))
            : BinaryPrimitives.ReadUInt64LittleEndian(data.Span.Slice(32, 8));
        public ulong UserDataOffset => Version < 4
            ? 0
            : BinaryPrimitives.ReadUInt64LittleEndian(data.Span.Slice(40, 8));

        public int Size => Version < 4 ? 32 : 48;
    }
}
