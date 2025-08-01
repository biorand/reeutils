using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Messages
{
    public unsafe class MsgFile(ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x47534D47;

        public ReadOnlyMemory<byte> Data => data;

        private ReadOnlySpan<T> GetSpan<T>(ulong offset, int count) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(data.Slice((int)offset, count * sizeof(T)).Span);
        }

        private MsgHeaderA HeaderA => MemoryMarshal.Read<MsgHeaderA>(data.Span);
        private MsgHeaderB HeaderB => MemoryMarshal.Read<MsgHeaderB>(data.Span.Slice((int)HeaderA.HeaderOffset));
        private MsgHeaderC HeaderC => MemoryMarshal.Read<MsgHeaderC>(data.Span.Slice((int)HeaderCOffset));
        private ReadOnlySpan<ulong> EntryOffsets => GetSpan<ulong>(HeaderCOffset + (ulong)sizeof(MsgHeaderC), (int)HeaderB.EntryCount);
        private ulong UnkData => MemoryMarshal.Read<ulong>(data.Span.Slice((int)HeaderC.UnkDataOffset));
        private ReadOnlySpan<int> Languages => GetSpan<int>(HeaderC.LangDataOffset, (int)HeaderB.LanguageCount);
        private ReadOnlySpan<int> AttributeTypes => GetSpan<int>(HeaderC.AttributeOffset, (int)HeaderB.AttributeCount);
        private ReadOnlySpan<ulong> AttributeNames => GetSpan<ulong>(HeaderC.AttributeNameOffset, (int)HeaderB.AttributeCount);

        private bool IsVersionEncrypt
        {
            get
            {
                var version = HeaderA.Version;
                return version > 12 && version != 0x2022033D;
            }
        }

        private bool IsVersionEntryByHash
        {
            get
            {
                var version = HeaderA.Version;
                return version > 15 && version != 0x2022033D;
            }
        }

        private ulong HeaderCOffset
        {
            get
            {
                var offset = HeaderA.HeaderOffset + (ulong)sizeof(MsgHeaderB);
                if (!IsVersionEncrypt)
                    offset -= 8;
                return offset;
            }
        }

        private ReadOnlySpan<MsgEntryHeader> Messages
        {
            get
            {
                var numLanguages = HeaderB.LanguageCount;
                var offsets = EntryOffsets;
                var messages = new MsgEntryHeader[offsets.Length];
                for (var i = 0; i < messages.Length; i++)
                {
                    var length = 0x20 + (numLanguages * sizeof(ulong));
                    messages[i] = new MsgEntryHeader(data.Slice((int)offsets[i], (int)length));
                }
                return messages;
            }
        }

        public Builder ToBuilder() => new Builder(this);

        public class Builder
        {
            public Builder(MsgFile msgFile)
            {
            }

            public MsgFile Build()
            {
                var ms = new MemoryStream();
                return new MsgFile(ms.ToArray());
            }
        }
    }
}
