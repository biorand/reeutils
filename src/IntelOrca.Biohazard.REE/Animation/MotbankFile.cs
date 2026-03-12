using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Animation
{
    public sealed class MotbankFile(ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x6B6E626D;

        public ReadOnlyMemory<byte> Data { get; } = data;

        public int Version => BinaryPrimitives.ReadInt32LittleEndian(Data.Span);

        private uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(Data.Span[4..]);
        private long HeaderBase => 8; // after version+magic (4+4)
        private long MotlistsOffset => BinaryPrimitives.ReadInt64LittleEndian(Data.Span[(int)(HeaderBase + 8 * 1)..]);
        private long UvarOffset => BinaryPrimitives.ReadInt64LittleEndian(Data.Span[(int)(HeaderBase + 8 * 2)..]);
        private long JmapOffset => Version >= 3 ? BinaryPrimitives.ReadInt64LittleEndian(Data.Span[(int)(HeaderBase + 8 * 3)..]) : 0;

        private int MotlistCount
        {
            get
            {
                var pos = (int)(HeaderBase + 8 * (Version >= 3 ? 4 : 2));
                return BinaryPrimitives.ReadInt32LittleEndian(Data.Span[pos..]);
            }
        }

        public string UvarPath => ReadWString(Data, (int)UvarOffset);
        public string JmapPath => Version >= 3 ? ReadWString(Data, (int)JmapOffset) : string.Empty;

        public ImmutableArray<MotlistItem> Items
        {
            get
            {
                if (MotlistsOffset == 0 || MotlistCount == 0)
                    return [];

                var result = ImmutableArray.CreateBuilder<MotlistItem>();
                var span = Data.Span;
                var pos = (int)MotlistsOffset;
                for (var i = 0; i < MotlistCount; i++)
                {
                    var offset = BinaryPrimitives.ReadInt64LittleEndian(span[pos..]);
                    pos += 8;
                    long bankId;
                    uint bankType = 0;
                    if (Version >= 3)
                    {
                        bankId = BinaryPrimitives.ReadInt32LittleEndian(span[pos..]);
                        pos += 4;
                        bankType = BinaryPrimitives.ReadUInt32LittleEndian(span[pos..]);
                        pos += 4;
                    }
                    else
                    {
                        bankId = BinaryPrimitives.ReadInt64LittleEndian(span[pos..]);
                        pos += 8;
                    }
                    var maskBits = BinaryPrimitives.ReadInt64LittleEndian(span[pos..]);
                    pos += 8;

                    var path = offset == 0 ? string.Empty : ReadWString(Data, (int)offset);
                    result.Add(new MotlistItem
                    {
                        Path = path,
                        BankId = bankId,
                        BankType = bankType,
                        MaskBits = maskBits,
                    });
                }

                return result.ToImmutable();
            }
        }

        private static string ReadWString(ReadOnlyMemory<byte> data, int offset)
        {
            if (offset == 0)
                return string.Empty;
            var span = MemoryMarshal.Cast<byte, char>(data.Span[offset..]);
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == '\0')
                    return new string(span[..i].ToArray());
            }
            return string.Empty;
        }

        public Builder ToBuilder()
        {
            return new Builder(this);
        }

        public sealed class MotlistItem
        {
            public long BankId { get; init; }
            public uint BankType { get; init; }
            public long MaskBits { get; init; }
            public string Path { get; init; } = string.Empty;
        }

        public sealed class Builder
        {
            public int Version { get; set; }
            public string UvarPath { get; set; } = "";
            public string JmapPath { get; set; } = "";
            public List<MotlistItem> Items { get; set; } = [];

            public Builder()
            {
            }

            public Builder(MotbankFile src)
            {
                Version = src.Version;
                UvarPath = src.UvarPath;
                JmapPath = src.JmapPath;
                Items = src.Items.ToList();
            }

            public MotbankFile Build()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                var headerStringPool = new StringPoolBuilder(ms);

                bw.Write(Version);
                bw.Write(MAGIC);
                bw.WriteZeros(8);

                var motlistsOffsetPlaceholderPos = ms.Position;
                bw.Write((long)0);

                if (!string.IsNullOrEmpty(UvarPath))
                    headerStringPool.WriteStringOffset64(UvarPath);
                else
                    bw.Write((long)0);

                if (Version >= 3)
                {
                    if (!string.IsNullOrEmpty(JmapPath))
                        headerStringPool.WriteStringOffset64(JmapPath);
                    else
                        bw.Write((long)0);
                }

                bw.Write(Items.Count);

                headerStringPool.WriteStrings();

                bw.Align(16);
                var motlistsOffset = ms.Position;

                var entryStringPool = new StringPoolBuilder(ms);
                foreach (var it in Items)
                {
                    entryStringPool.WriteStringOffset64(it.Path ?? string.Empty);
                    if (Version >= 3)
                    {
                        bw.Write((int)it.BankId);
                        bw.Write(it.BankType);
                    }
                    else
                    {
                        bw.Write((long)it.BankId);
                    }
                    bw.Write((long)it.MaskBits);
                }

                // backfill motlists offset
                var cur = ms.Position;
                ms.Position = motlistsOffsetPlaceholderPos;
                bw.Write(motlistsOffset);
                ms.Position = cur;

                entryStringPool.WriteStrings();

                return new MotbankFile(ms.ToArray());
            }
        }
    }
}
