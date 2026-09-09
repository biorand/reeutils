using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE
{
    /// <summary>
    /// Parser for RE Engine collider-set (.cset) files. A CSET container carries a native header plus
    /// the zone's collider geometry, followed by an embedded RSZ stream holding the per-zone parameter
    /// objects (e.g. <c>app.col_user_data.RestrictZoneCollider</c>). The header/geometry bytes are not
    /// RSZ-derivable, so they are preserved verbatim across a JSON round-trip; only the RSZ stream is
    /// decoded and re-emitted. Onimusha Way of the Sword ships these as .cset.6.
    /// </summary>
    public sealed class CsetFile
    {
        public const uint Magic = 0x54455343; // "CSET"

        private const int SlotScanEnd = 0x60;

        private readonly ReadOnlyMemory<byte> _data;

        public ReadOnlyMemory<byte> Data => _data;
        public int Version { get; }

        /// <summary>Counts at 0x08/0x0C; collider-shape list sizes, exposed for inspection.</summary>
        public int ShapeListCount { get; }
        public int ShapeCount { get; }

        /// <summary>Absolute offset of the parameter RSZ stream.</summary>
        public int MainRszOffset { get; }

        /// <summary>Everything before the main RSZ (header + collider geometry), preserved verbatim.</summary>
        public byte[] Prefix { get; }

        /// <summary>Everything after the main RSZ (trailing gap, secondary RSZ), preserved verbatim.</summary>
        public byte[] Tail { get; }

        /// <summary>Offset of the next embedded RSZ stream within <see cref="Tail"/>, or -1 if none.</summary>
        public int SecondaryRszOffsetInTail { get; }

        /// <summary>8-byte-aligned header slots that referenced a section boundary in the source file,
        /// keyed by their byte offset; values are patched on rebuild if a section moves.</summary>
        public IReadOnlyDictionary<int, long> BoundarySlots { get; }

        public int InstanceCount => MainRsz.InstanceCount;
        public int RszVersion => MainRsz.Version;

        public RszFile MainRsz { get; }

        public CsetFile(int version, ReadOnlyMemory<byte> data, RszTypeRepository? repository = null)
        {
            _data = data;
            Version = version;

            if (data.Length < 0x10)
                throw new InvalidDataException("File too small to be CSET");
            if (BinaryPrimitives.ReadUInt32LittleEndian(data.Span) != Magic)
                throw new InvalidDataException($"Invalid CSET magic: 0x{BinaryPrimitives.ReadUInt32LittleEndian(data.Span):X8}");

            var span = data.Span;
            ShapeListCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0x08, 4));
            ShapeCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0x0C, 4));

            var (mainStart, mainEnd) = FindMainRsz(data, repository);
            if (mainStart <= 0)
                throw new InvalidDataException("Could not find an RSZ stream in CSET file.");

            MainRszOffset = mainStart;
            Prefix = data.Slice(0, mainStart).ToArray();
            Tail = data.Slice(mainEnd, data.Length - mainEnd).ToArray();
            MainRsz = new RszFile(data.Slice(mainStart, mainEnd - mainStart));
            SecondaryRszOffsetInTail = FindNextRsz(Tail);

            BoundarySlots = ScanBoundarySlots(span, mainStart, mainEnd, data.Length);
        }

        private static int FindNextRsz(byte[] tail)
        {
            for (var i = 0; i <= tail.Length - 4; i++)
            {
                if (tail[i] == 0x52 && tail[i + 1] == 0x53 && tail[i + 2] == 0x5A && tail[i + 3] == 0x00)
                    return i;
            }
            return -1;
        }

        public CsetFile(ReadOnlyMemory<byte> data, RszTypeRepository? repository = null) : this(VersionFromData(data), data, repository)
        {
        }

        public CsetFile(string path, RszTypeRepository? repository = null) : this(File.ReadAllBytes(path), repository)
        {
        }

        private static int VersionFromData(ReadOnlyMemory<byte> data) =>
            data.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(4, 4)) : -1;

        private static (int Start, int End) FindMainRsz(ReadOnlyMemory<byte> data, RszTypeRepository? repository)
        {
            var bytes = data.ToArray();
            // Candidate: an RSZ-magic position whose header is plausible. A candidate is "stable" if
            // rebuilding it reproduces the source bytes exactly (so its length is trustworthy). The
            // parameter stream is the earliest candidate that decodes with objects, even if it is not
            // byte-stable -- a mismatched type CRC in the RSZ dump makes some retail files rebuild
            // non-identically while still carrying the real object list.
            var candidates = new List<(int start, int len, bool hasObjects, bool stable)>();
            for (var pos = 0; pos <= bytes.Length - 48; pos++)
            {
                if (bytes[pos] != 0x52 || bytes[pos + 1] != 0x53 || bytes[pos + 2] != 0x5A || bytes[pos + 3] != 0x00)
                    continue;

                var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(pos + 4, 4));
                if (version is not (3 or 4 or 8 or 16))
                    continue;
                var objectCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(pos + 8, 4));
                var instanceCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(pos + 12, 4));
                if (instanceCount == 0 || instanceCount > 100_000 || objectCount > instanceCount)
                    continue;

                var len = 0;
                var stable = false;
                if (repository != null)
                {
                    len = TryGetIdenticalLength(bytes, pos, repository, out stable);
                }
                if (len > 0)
                {
                    candidates.Add((pos, len, objectCount > 0, stable));
                }
            }

            // Prefer the earliest candidate that decodes with objects (the parameter stream); otherwise
            // the earliest stable stream.
            (int start, int len, bool hasObjects, bool stable) chosen = candidates.Count > 0 ? candidates[0] : (0, 0, false, false);
            foreach (var c in candidates)
            {
                if (c.hasObjects)
                {
                    chosen = c;
                    break;
                }
            }
            if (chosen.start == 0)
            {
                foreach (var c in candidates)
                {
                    if (c.stable)
                    {
                        chosen = c;
                        break;
                    }
                }
            }
            return chosen.start == 0 ? (0, 0) : (chosen.start, chosen.start + chosen.len);
        }

        /// <summary>
        /// For an RSZ candidate, rebuilds it (aligning against its absolute position) and returns its
        /// length if the rebuild is valid; <paramref name="stable"/> reports whether the rebuild also
        /// reproduces the source bytes exactly. Returns 0 when the candidate cannot be decoded.
        /// </summary>
        private static int TryGetIdenticalLength(byte[] data, int pos, RszTypeRepository repository, out bool stable)
        {
            stable = false;
            try
            {
                var rsz = new RszFile(data.AsMemory(pos));
                var builder = rsz.ToBuilder(repository);
                builder.AlignOffset = pos;
                var rebuilt = builder.Build();
                var length = rebuilt.Data.Length;
                stable = pos + length <= data.Length && rebuilt.Data.Span.SequenceEqual(data.AsSpan(pos, length));
                return length;
            }
            catch
            {
                return 0;
            }
        }

        public ImmutableArray<RszObjectNode> ReadObjects(RszTypeRepository repository) => MainRsz.ReadObjectList(repository);

        public Builder ToBuilder(RszTypeRepository repository) => new(repository, this);

        private static Dictionary<int, long> ScanBoundarySlots(ReadOnlySpan<byte> span, int mainStart, int mainEnd, int fileLength)
        {
            // Record 8-byte-aligned fields in the header region that point at a section boundary so a
            // rebuild can move them if the RSZ stream changes length. Only the header area is scanned
            // to avoid matching geometry bytes by coincidence.
            var boundaryValues = new HashSet<long> { mainStart, mainEnd, fileLength };
            var slots = new Dictionary<int, long>();
            for (var offset = 0x08; offset + 8 <= Math.Min(mainStart, SlotScanEnd); offset += 8)
            {
                var value = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8));
                if (boundaryValues.Contains(value))
                {
                    slots[offset] = value;
                }
            }
            return slots;
        }

        public sealed class Builder
        {
            public RszTypeRepository Repository { get; }
            public int Version { get; set; }
            public byte[] Prefix { get; set; } = [];
            public byte[] Tail { get; set; } = [];
            public ImmutableArray<RszObjectNode> Objects { get; set; } = [];
            public IReadOnlyDictionary<int, long> BoundarySlots { get; set; } = new Dictionary<int, long>();

            /// <summary>Original file length (to classify header slots by old section boundary).</summary>
            public int OldFileLength { get; set; }

            /// <summary>Offset of the next embedded RSZ stream within <see cref="Tail"/> (or -1).</summary>
            public int SecondaryRszOffsetInTail { get; set; } = -1;

            public Builder(RszTypeRepository repository, int version)
            {
                Repository = repository;
                Version = version;
            }

            public Builder(RszTypeRepository repository, CsetFile instance)
            {
                Repository = repository;
                Version = instance.Version;
                Prefix = instance.Prefix;
                Tail = instance.Tail;
                BoundarySlots = instance.BoundarySlots;
                Objects = instance.ReadObjects(repository);
                OldFileLength = instance.Data.Length;
                SecondaryRszOffsetInTail = instance.SecondaryRszOffsetInTail;
            }

            public CsetFile Build()
            {
                var mainStart = Prefix.Length;
                var mainRsz = new RszFile.Builder(Repository, 16) { Objects = Objects }.Build();

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                bw.Write(Prefix.AsSpan());
                bw.Write(mainRsz.Data.Span);
                var newMainEnd = mainStart + mainRsz.Data.Length;
                bw.Write(Tail.AsSpan());
                var newFileLength = newMainEnd + Tail.Length;

                // Patch header slots that referenced a section boundary. The main-RSZ start is fixed
                // (the prefix never changes); the main-RSZ end, the trailing (secondary) RSZ and EOF
                // move if the RSZ length changed.
                var oldMainEnd = OldFileLength - Tail.Length;
                var oldSecondaryStart = SecondaryRszOffsetInTail >= 0 ? oldMainEnd + SecondaryRszOffsetInTail : -1;
                var newSecondaryStart = SecondaryRszOffsetInTail >= 0 ? newMainEnd + SecondaryRszOffsetInTail : -1;
                foreach (var (offset, oldValue) in BoundarySlots)
                {
                    long newValue;
                    if (oldValue == mainStart)
                        newValue = mainStart;
                    else if (oldValue == oldMainEnd)
                        newValue = newMainEnd;
                    else if (oldSecondaryStart >= 0 && oldValue == oldSecondaryStart)
                        newValue = newSecondaryStart;
                    else if (oldValue == OldFileLength)
                        newValue = newFileLength;
                    else
                        continue;
                    ms.Position = offset;
                    bw.Write(newValue);
                }

                return new CsetFile(Version, ms.ToArray(), Repository);
            }
        }
    }
}