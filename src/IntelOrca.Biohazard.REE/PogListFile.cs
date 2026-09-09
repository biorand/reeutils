using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard.REE
{
    /// <summary>
    /// Parser for RE Engine point-graph list (.poglst) files. A PGL container is a simple index: a
    /// native header followed by a table of absolute string offsets pointing at UTF-16 paths of the
    /// <c>.pog</c> point-graph files a <c>ContextLayouter</c> / <c>RandomSetPointFinder</c> loads.
    /// Onimusha Way of the Sword ships these as .poglst.0 (version 0).
    /// </summary>
    public sealed class PogListFile
    {
        public const uint Magic = 0x004C4750; // "PGL\0"

        private readonly ReadOnlyMemory<byte> _data;

        public ReadOnlyMemory<byte> Data => _data;
        public int Version { get; }

        /// <summary>4-byte hash field at 0x0C; uninitialized (0xCDCDCDCD) in retail files, preserved verbatim.</summary>
        public uint Hash { get; }

        public ImmutableArray<string> PogFiles { get; }

        public PogListFile(int version, ReadOnlyMemory<byte> data)
        {
            _data = data;
            Version = version;

            if (data.Length < 0x18)
                throw new InvalidDataException("File too small to be PGL");
            if (BinaryPrimitives.ReadUInt32LittleEndian(data.Span) != Magic)
                throw new InvalidDataException($"Invalid PGL magic: 0x{BinaryPrimitives.ReadUInt32LittleEndian(data.Span):X8}");

            var span = data.Span;
            var count = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0x08, 4));
            Hash = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x0C, 4));
            var offsetsOffset = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(0x10, 8));

            if (count < 0 || (count > 0 && (offsetsOffset < 0x18 || offsetsOffset + (long)count * 8 > data.Length)))
                throw new InvalidDataException($"Invalid PGL count {count} / offsets offset 0x{offsetsOffset:X}.");

            var paths = ImmutableArray.CreateBuilder<string>(count);
            for (var i = 0; i < count; i++)
            {
                var pathOffset = BinaryPrimitives.ReadInt64LittleEndian(span.Slice((int)offsetsOffset + i * 8, 8));
                paths.Add(ReadUtf16String(span, pathOffset));
            }
            PogFiles = paths.MoveToImmutable();
        }

        public PogListFile(ReadOnlyMemory<byte> data) : this(VersionFromData(data), data)
        {
        }

        public PogListFile(string path) : this(File.ReadAllBytes(path))
        {
        }

        private static int VersionFromData(ReadOnlyMemory<byte> data) =>
            data.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(4, 4)) : -1;

        private static string ReadUtf16String(ReadOnlySpan<byte> span, long offset)
        {
            if (offset < 0 || offset >= span.Length)
                return string.Empty;

            var chars = MemoryMarshal.Cast<byte, char>(span.Slice((int)offset));
            var end = 0;
            while (end < chars.Length && chars[end] != '\0')
                end++;
            return new string(chars.Slice(0, end).ToArray());
        }

        public Builder ToBuilder() => new(this);

        public sealed class Builder
        {
            public int Version { get; set; }
            public uint Hash { get; set; }
            public List<string> PogFiles { get; } = [];

            public Builder()
            {
            }

            public Builder(PogListFile instance)
            {
                Version = instance.Version;
                Hash = instance.Hash;
                PogFiles.AddRange(instance.PogFiles);
            }

            public PogListFile Build()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                var offsetsOffset = PogFiles.Count == 0 ? 0L : 0x18L;
                bw.Write(Magic);
                bw.Write(Version);
                bw.Write(PogFiles.Count);
                bw.Write(Hash);
                bw.Write(offsetsOffset);

                if (PogFiles.Count == 0)
                    return new PogListFile(Version, ms.ToArray());

                var offsetPositions = new long[PogFiles.Count];
                for (var i = 0; i < PogFiles.Count; i++)
                {
                    offsetPositions[i] = ms.Position;
                    bw.Write(0L); // placeholder
                }

                // Strings are written at 8-byte-aligned offsets; the alignment gap between entries is
                // zero padding. The final string ends at EOF (no trailing pad).
                for (var i = 0; i < PogFiles.Count; i++)
                {
                    Align8(ms);
                    var pos = ms.Position;
                    ms.Position = offsetPositions[i];
                    bw.Write((long)pos);
                    ms.Position = pos;
                    bw.Write(Encoding.Unicode.GetBytes(PogFiles[i]));
                    bw.Write((ushort)0);
                }

                return new PogListFile(Version, ms.ToArray());
            }

            private static void Align8(MemoryStream ms)
            {
                var rem = ms.Position % 8;
                if (rem != 0)
                {
                    var pad = new byte[8 - rem];
                    ms.Write(pad, 0, pad.Length);
                }
            }
        }
    }
}