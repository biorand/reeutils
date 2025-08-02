using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Messages
{
    public unsafe class MsgFile(ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x47534D47;

        private StringData? _cachedStringData;

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
        private ReadOnlySpan<LanguageId> Languages => GetSpan<LanguageId>(HeaderC.LangDataOffset, (int)HeaderB.LanguageCount);
        private ReadOnlySpan<int> AttributeTypes => GetSpan<int>(HeaderC.AttributeOffset, (int)HeaderB.AttributeCount);
        private ReadOnlySpan<ulong> AttributeNames => GetSpan<ulong>(HeaderC.AttributeNameOffset, (int)HeaderB.AttributeCount);

        private static bool IsVersionEncrypt(int version)
        {
            return version > 12 && version != 0x2022033D;
        }

        private static bool IsVersionEntryByHash(int version)
        {
            return version > 15 && version != 0x2022033D;
        }

        private ulong HeaderCOffset
        {
            get
            {
                var offset = HeaderA.HeaderOffset + (ulong)sizeof(MsgHeaderB);
                if (!IsVersionEncrypt((int)HeaderA.Version))
                    offset -= 8;
                return offset;
            }
        }

        private ReadOnlySpan<MsgEntryHeader> Messages
        {
            get
            {
                var numLanguages = (int)HeaderB.LanguageCount;
                var entrySize = MsgEntryHeader.GetSize(numLanguages);
                var offsets = EntryOffsets;
                var messages = new MsgEntryHeader[offsets.Length];
                for (var i = 0; i < messages.Length; i++)
                {
                    messages[i] = new MsgEntryHeader(data.Slice((int)offsets[i], entrySize));
                }
                return messages;
            }
        }

        private ulong StringDataOffset
        {
            get
            {
                return HeaderB.DataOffset;
            }
        }

        private StringData GetStringData()
        {
            var result = _cachedStringData;
            if (!result.HasValue)
            {
                var stringDataOffset = StringDataOffset;
                var encrypted = Data.Slice((int)stringDataOffset);
                result = StringData.Decrypt(encrypted.Span, stringDataOffset);
                _cachedStringData = result;
            }
            return result.Value;
        }

        private string GetString(ulong offset)
        {
            var stringData = GetStringData();
            return stringData.GetString(offset);
        }

        public int Count => (int)HeaderB.EntryCount;

        public Msg GetMessage(int index)
        {
            var stringData = GetStringData();
            var languageIds = Languages;
            var msgHeader = Messages[index];
            var values = ImmutableArray.CreateBuilder<MsgValue>(Languages.Length);
            for (var i = 0; i < languageIds.Length; i++)
            {
                values.Add(
                    new MsgValue(
                        languageIds[i],
                        stringData.GetString(msgHeader.ContentOffsets[i])));
            }

            return new Msg
            {
                Guid = msgHeader.Guid,
                Crc = (int)msgHeader.Crc,
                Name = GetString(msgHeader.EntryName),
                Values = values.ToImmutable()
            };
        }

        public Builder ToBuilder() => new Builder(this);

        public class Builder
        {
            public int Version { get; set; }
            public ImmutableArray<LanguageId> Languages { get; set; }
            public List<Msg> Messages { get; } = [];

            public Builder(MsgFile msgFile)
            {
                Version = (int)msgFile.HeaderA.Version;
                Languages = msgFile.Languages.ToImmutableArray();

                var stringData = msgFile.GetStringData();
                var messages = msgFile.Messages;
                var languageIds = msgFile.Languages;
                foreach (var msgHeader in messages)
                {
                    var values = ImmutableArray.CreateBuilder<MsgValue>();
                    for (var i = 0; i < languageIds.Length; i++)
                    {
                        values.Add(
                            new MsgValue(
                                languageIds[i],
                                stringData.GetString(msgHeader.ContentOffsets[i])));
                    }
                    Messages.Add(new Msg
                    {
                        Guid = msgHeader.Guid,
                        Name = msgFile.GetString(msgHeader.EntryName),
                        Values = values.ToImmutable()
                    });
                }
            }

            public MsgFile Build()
            {
                var headerA = new MsgHeaderA()
                {
                    Magic = MAGIC,
                    Version = (uint)Version
                };
                var headerB = new MsgHeaderB()
                {
                    EntryCount = (uint)Messages.Count,
                    LanguageCount = (uint)Languages.Length
                };
                var headerC = new MsgHeaderC();
                var messageHeaderOffsets = new ulong[Messages.Count];

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                // Calculate offsets
                ms.Position += sizeof(MsgHeaderA);
                headerA.HeaderOffset = (ulong)ms.Position;
                ms.Position += sizeof(MsgHeaderB);
                if (!IsVersionEncrypt(Version))
                {
                    ms.Position -= 8;
                }
                var headerCoffset = (ulong)ms.Position;
                ms.Position += sizeof(MsgHeaderC);
                ms.Position += Messages.Count * sizeof(ulong);
                headerC.UnkDataOffset = (ulong)ms.Position;
                ms.Position += 8;
                headerC.LangDataOffset = (ulong)ms.Position;
                for (var i = 0; i < Languages.Length; i++)
                {
                    bw.Write((int)Languages[i]);
                }
                bw.Align(8);
                headerC.AttributeOffset = (ulong)ms.Position;
                headerC.AttributeNameOffset = (ulong)ms.Position;

                var entrySize = MsgEntryHeader.GetSize(Languages.Length);
                for (var i = 0; i < Messages.Count; i++)
                {
                    messageHeaderOffsets[i] = (ulong)ms.Position;
                    ms.Position += entrySize;
                }

                var attributeStartOffset = (ulong)ms.Position;

                headerB.DataOffset = (ulong)ms.Position;
                var stringDataBuilder = new StringData.Builder(headerB.DataOffset);

                // Write data
                ms.Position = 0;
                bw.Write(headerA);
                ms.Position = (int)headerA.HeaderOffset;
                bw.Write(headerB);
                ms.Position = (int)headerCoffset;
                bw.Write(headerC);
                for (var i = 0; i < Messages.Count; i++)
                {
                    bw.Write(messageHeaderOffsets[i]);
                }
                for (var i = 0; i < Messages.Count; i++)
                {
                    ms.Position = (int)messageHeaderOffsets[i];

                    var m = Messages[i];
                    bw.Write(m.Guid);
                    bw.Write(m.Crc);
                    if (IsVersionEntryByHash(Version))
                        bw.Write(MurMur3.HashData(m.Name));
                    else
                        bw.Write(i);
                    bw.Write(stringDataBuilder.AddString(m.Name));
                    bw.Write(attributeStartOffset);
                    for (var j = 0; j < Languages.Length; j++)
                    {
                        var languageId = Languages[j];
                        bw.Write(stringDataBuilder.AddString(m[languageId]));
                    }
                }
                ms.Position = (int)headerB.DataOffset;
                bw.Write(stringDataBuilder.Build());
                return new MsgFile(ms.ToArray());
            }
        }

        private readonly struct StringData(ReadOnlyMemory<byte> decrypted, ulong baseOffset)
        {
            private static readonly byte[] g_encryptionKey = [0xCF, 0xCE, 0xFB, 0xF8, 0xEC, 0x0A, 0x33, 0x66, 0x93, 0xA9, 0x1D, 0x93, 0x50, 0x39, 0x5F, 0x09];

            public static StringData Decrypt(ReadOnlySpan<byte> data, ulong baseOffset)
            {
                var decrypted = new byte[data.Length];
                var prev = (byte)0;
                for (var i = 0; i < data.Length; i++)
                {
                    var key = g_encryptionKey[i % g_encryptionKey.Length];
                    var ch = (byte)(data[i] ^ prev ^ key);
                    decrypted[i] = ch;
                    prev = data[i];
                }
                return new StringData(decrypted, baseOffset);
            }

            public string GetString(ulong offset)
            {
                if (offset >= baseOffset)
                {
                    var startOffset = (int)(offset - baseOffset);
                    var data = MemoryMarshal.Cast<byte, char>(decrypted.Slice(startOffset).Span);
                    for (var i = 0; i < data.Length; i++)
                    {
                        if (data[i] == '\0')
                        {
                            return new string(data.Slice(0, i).ToArray());
                        }
                    }
                }
                return "";
            }

            public class Builder(ulong baseOffset)
            {
                private readonly Dictionary<string, ulong> _cache = [];
                private readonly List<byte> _data = [];
                private byte _prev;

                private void AddByte(byte value)
                {
                    var encrypted = (byte)(value ^ _prev ^ g_encryptionKey[_data.Count % g_encryptionKey.Length]);
                    _data.Add(encrypted);
                    _prev = encrypted;
                }

                public ulong AddString(string value)
                {
                    if (!_cache.TryGetValue(value, out var result))
                    {
                        var offset = _data.Count;
                        var rawStringData = Encoding.Unicode.GetBytes(value);
                        for (var i = 0; i < rawStringData.Length; i++)
                        {
                            AddByte(rawStringData[i]);
                        }
                        AddByte(0);
                        AddByte(0);
                        result = baseOffset + (ulong)offset;
                        _cache.Add(value, result);
                    }
                    return result;
                }

                public byte[] Build()
                {
                    return _data.ToArray();
                }
            }
        }
    }
}
