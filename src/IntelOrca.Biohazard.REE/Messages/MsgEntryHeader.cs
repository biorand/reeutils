using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Messages
{
    internal readonly struct MsgEntryHeader(ReadOnlyMemory<byte> data)
    {
        public Guid Guid => MemoryMarshal.Read<Guid>(data.Span);
        public uint Crc => MemoryMarshal.Read<uint>(data.Slice(0x10).Span);
        public uint Hash => MemoryMarshal.Read<uint>(data.Slice(0x14).Span);
        public uint Index => MemoryMarshal.Read<uint>(data.Slice(0x14).Span);
        public ulong EntryName => MemoryMarshal.Read<ulong>(data.Slice(0x18).Span);
        public ulong AttributeOffset => MemoryMarshal.Read<ulong>(data.Slice(0x1C).Span);
        public ReadOnlySpan<ulong> ContentOffsets => MemoryMarshal.Cast<byte, ulong>(data.Slice(0x20).Span);
    }
}
