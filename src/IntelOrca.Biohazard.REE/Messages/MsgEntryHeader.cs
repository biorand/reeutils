using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Messages
{
    [DebuggerDisplay("{Guid}")]
    internal readonly struct MsgEntryHeader(ReadOnlyMemory<byte> data)
    {
        public Guid Guid => MemoryMarshal.Read<Guid>(data.Span);
        public uint Crc => MemoryMarshal.Read<uint>(data.Slice(0x10).Span);
        public uint Hash => MemoryMarshal.Read<uint>(data.Slice(0x14).Span);
        public uint Index => MemoryMarshal.Read<uint>(data.Slice(0x14).Span);
        public ulong EntryName => MemoryMarshal.Read<ulong>(data.Slice(0x18).Span);
        public ulong AttributeOffset => MemoryMarshal.Read<ulong>(data.Slice(0x20).Span);
        public ReadOnlySpan<ulong> ContentOffsets => MemoryMarshal.Cast<byte, ulong>(data.Slice(0x28).Span);

        public static int GetSize(int languageCount)
        {
            return 0x28 + (languageCount * sizeof(ulong));
        }
    }
}
