using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE
{
    /// <summary>
    /// Parser for RE Engine point-graph (.pog) files. A POG container wraps a small native header, a
    /// per-kind node section, and up to two embedded RSZ streams: the "main" stream holds the
    /// placed-context objects (e.g. <c>app.ContextPointGraphEnemy</c> with its transform), the
    /// "secondary" stream holds the graph-level container object (e.g.
    /// <c>app.ContextPointGraphEnemy.cContextLayoutGraphEnemy</c>).
    /// <para>
    /// Two node-section layouts are seen in the wild: the standard layout (header field 0x10 == 0)
    /// carries a 16-byte-per-node table whose first u32 is the node's position in the main RSZ object
    /// list; the spawner/point-pool layout (0x10 != 0, e.g. <c>SpnSet_*</c>, <c>RandomSetPoint_*</c>)
    /// carries a name table plus string pool that is preserved verbatim. An empty graph (no nodes)
    /// has no main RSZ stream at all. Onimusha Way of the Sword ships these as .pog.12 (version 12).
    /// </summary>
    public sealed class PogFile
    {
        public const uint Magic = 0x00474F50; // "POG\0"

        private const int NodeSectionOffset = 0x58;
        private const int NodeEntrySize = 16;

        private readonly ReadOnlyMemory<byte> _data;

        public ReadOnlyMemory<byte> Data => _data;
        public int Version { get; }

        /// <summary>8-byte field at 0x08; a file-specific hash, preserved verbatim.</summary>
        public ulong Hash { get; }

        /// <summary>4-byte field at 0x10; 0 for the standard node-table layout, non-zero for the
        /// spawner/point-pool layout (name table).</summary>
        public uint Unknown { get; }

        /// <summary>Point-graph id hash at 0x18; shared between a fixed set and its RandomSet variants
        /// (matches <c>app.user_data.SetWaveUserData._PointGraphId</c>).</summary>
        public uint GraphHash { get; }

        public int NodeCount { get; }

        private long MainRszStart { get; }
        private long SecondaryRszStart { get; }
        private long EndOffset { get; }

        /// <summary>Whether the file carries a main (placed-context) RSZ stream. False for empty graphs.</summary>
        public bool HasMainRsz { get; }

        /// <summary>Verbatim bytes of the node section [0x58, main-RSZ start). For the standard layout
        /// this equals the node table; for the spawner/point-pool layout it is an opaque name table.</summary>
        public byte[] NodeSectionBytes { get; }

        /// <summary>Parsed node-table entries (standard layout only; empty for the spawner layout).</summary>
        public ImmutableArray<PogNodeEntry> NodeEntries { get; }

        /// <summary>Bytes between the main RSZ stream and the secondary RSZ stream (a short gap,
        /// preserved verbatim for byte-identical rebuilds).</summary>
        public byte[] MiddleBytes { get; }

        /// <summary>Header slots at 0x20..0x50, preserved from the source. Slots 3-6 (section offsets)
        /// are recomputed on rebuild.</summary>
        public ImmutableArray<long> HeaderSlots { get; }

        public int InstanceCount => HasMainRsz ? MainRsz.InstanceCount : 0;
        public int RszVersion => HasMainRsz ? MainRsz.Version : 16;

        public RszFile MainRsz { get; }
        public RszFile SecondaryRsz { get; }

        public PogFile(int version, ReadOnlyMemory<byte> data)
        {
            _data = data;
            Version = version;

            if (data.Length < 0x58)
                throw new InvalidDataException("File too small to be POG");
            if (BinaryPrimitives.ReadUInt32LittleEndian(data.Span) != Magic)
                throw new InvalidDataException($"Invalid POG magic: 0x{BinaryPrimitives.ReadUInt32LittleEndian(data.Span):X8}");

            var span = data.Span;
            Hash = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x08, 8));
            Unknown = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x10, 4));
            NodeCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0x14, 4));
            GraphHash = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x18, 4));
            HeaderSlots = ReadHeaderSlots(span);

            var mainRszStart = HeaderSlots[3];
            SecondaryRszStart = HeaderSlots[5];
            EndOffset = HeaderSlots[6];
            HasMainRsz = mainRszStart > 0;

            if (NodeCount < 0)
                throw new InvalidDataException($"Invalid POG node count {NodeCount}.");

            var nodeSectionEnd = HasMainRsz ? mainRszStart : (long)NodeSectionOffset;
            if (nodeSectionEnd < NodeSectionOffset || nodeSectionEnd > data.Length)
                throw new InvalidDataException($"Invalid POG node section end 0x{nodeSectionEnd:X}.");
            if (SecondaryRszStart < nodeSectionEnd || SecondaryRszStart > data.Length)
                throw new InvalidDataException($"Invalid POG secondary RSZ offset 0x{SecondaryRszStart:X}.");
            if (EndOffset < SecondaryRszStart || EndOffset > data.Length)
                throw new InvalidDataException($"Invalid POG end offset 0x{EndOffset:X}.");

            NodeSectionBytes = data.Slice(NodeSectionOffset, (int)(nodeSectionEnd - NodeSectionOffset)).ToArray();

            if (Unknown == 0)
            {
                // Standard layout: the node section is a nodeCount x 16 node table.
                if (NodeCount * NodeEntrySize != NodeSectionBytes.Length)
                    throw new InvalidDataException($"POG node section size {NodeSectionBytes.Length} does not match node count {NodeCount}.");
                var entries = ImmutableArray.CreateBuilder<PogNodeEntry>(NodeCount);
                for (var i = 0; i < NodeCount; i++)
                {
                    entries.Add(PogNodeEntry.Read(span.Slice(NodeSectionOffset + i * NodeEntrySize, NodeEntrySize)));
                }
                NodeEntries = entries.MoveToImmutable();
            }
            else
            {
                NodeEntries = [];
            }

            var mainRszEnd = HasMainRsz
                ? HeaderSlots[4]
                : nodeSectionEnd;
            if (HasMainRsz && (mainRszEnd < mainRszStart || mainRszEnd > SecondaryRszStart))
                throw new InvalidDataException($"Invalid POG main RSZ range 0x{mainRszStart:X}-0x{mainRszEnd:X}.");

            MainRszStart = mainRszStart;
            MiddleBytes = data.Slice((int)mainRszEnd, (int)(SecondaryRszStart - mainRszEnd)).ToArray();
            MainRsz = HasMainRsz
                ? new RszFile(data.Slice((int)mainRszStart, (int)(mainRszEnd - mainRszStart)))
                : new RszFile(ReadOnlyMemory<byte>.Empty);
            SecondaryRsz = new RszFile(data.Slice((int)SecondaryRszStart, (int)(EndOffset - SecondaryRszStart)));
        }

        public PogFile(ReadOnlyMemory<byte> data) : this(VersionFromData(data), data)
        {
        }

        public PogFile(string path) : this(File.ReadAllBytes(path))
        {
        }

        private static int VersionFromData(ReadOnlyMemory<byte> data) =>
            data.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(4, 4)) : -1;

        private static ImmutableArray<long> ReadHeaderSlots(ReadOnlySpan<byte> span)
        {
            var slots = ImmutableArray.CreateBuilder<long>(7);
            for (var i = 0; i < 7; i++)
            {
                slots.Add(BinaryPrimitives.ReadInt64LittleEndian(span.Slice(0x20 + i * 8, 8)));
            }
            return slots.MoveToImmutable();
        }

        public ImmutableArray<RszObjectNode> ReadObjects(RszTypeRepository repository) =>
            HasMainRsz ? MainRsz.ReadObjectList(repository) : [];

        public ImmutableArray<RszObjectNode> ReadGraphObjects(RszTypeRepository repository) => SecondaryRsz.ReadObjectList(repository);

        public Builder ToBuilder(RszTypeRepository repository) => new(repository, this);

        /// <summary>
        /// One 16-byte node-table slot (standard layout). The <see cref="ObjectIndex"/> is a position in
        /// the main RSZ object list; the remaining 12 bytes are reserved (zero in every observed file)
        /// but carried verbatim so untouched files rebuild byte-identically.
        /// </summary>
        public readonly record struct PogNodeEntry(uint ObjectIndex, uint ReservedA, ulong ReservedB)
        {
            public static PogNodeEntry Read(ReadOnlySpan<byte> span) => new(
                BinaryPrimitives.ReadUInt32LittleEndian(span),
                BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4)),
                BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(8, 8)));
        }

        public sealed class Builder
        {
            public RszTypeRepository Repository { get; }
            public int Version { get; set; }
            public ulong Hash { get; set; }
            public uint Unknown { get; set; }
            public uint GraphHash { get; set; }
            public int NodeCount { get; set; }
            public ImmutableArray<PogNodeEntry> NodeEntries { get; set; } = [];
            public byte[] NodeSectionBytes { get; set; } = [];
            public byte[] MiddleBytes { get; set; } = [];
            public ImmutableArray<long> HeaderSlots { get; set; } = [];
            public bool HasMainRsz { get; set; }
            public ImmutableArray<RszObjectNode> Objects { get; set; } = [];
            public ImmutableArray<RszObjectNode> GraphObjects { get; set; } = [];

            public Builder(RszTypeRepository repository, int version)
            {
                Repository = repository;
                Version = version;
            }

            public Builder(RszTypeRepository repository, PogFile instance)
            {
                Repository = repository;
                Version = instance.Version;
                Hash = instance.Hash;
                Unknown = instance.Unknown;
                GraphHash = instance.GraphHash;
                NodeCount = instance.NodeCount;
                NodeEntries = instance.NodeEntries;
                NodeSectionBytes = instance.NodeSectionBytes;
                MiddleBytes = instance.MiddleBytes;
                HeaderSlots = instance.HeaderSlots;
                HasMainRsz = instance.HasMainRsz;
                Objects = instance.ReadObjects(repository);
                GraphObjects = instance.ReadGraphObjects(repository);
            }

            public PogFile Build()
            {
                // Rebuild the node section: for the standard layout it is regenerated from the node
                // table (so nodes can be added/removed); otherwise the source bytes are preserved.
                byte[] nodeSection;
                if (Unknown == 0)
                {
                    nodeSection = new byte[NodeEntries.Length * NodeEntrySize];
                    for (var i = 0; i < NodeEntries.Length; i++)
                    {
                        var entry = NodeEntries[i];
                        BinaryPrimitives.WriteUInt32LittleEndian(nodeSection.AsSpan(i * NodeEntrySize, 4), entry.ObjectIndex);
                        BinaryPrimitives.WriteUInt32LittleEndian(nodeSection.AsSpan(i * NodeEntrySize + 4, 4), entry.ReservedA);
                        BinaryPrimitives.WriteUInt64LittleEndian(nodeSection.AsSpan(i * NodeEntrySize + 8, 8), entry.ReservedB);
                    }
                }
                else
                {
                    nodeSection = NodeSectionBytes;
                }

                var mainRszStart = NodeSectionOffset + nodeSection.Length;
                var emitMainRsz = HasMainRsz || Objects.Length > 0;
                var rszVersion = 16;
                RszFile? mainRsz = null;
                long mainRszEnd;
                if (emitMainRsz)
                {
                    var mainRszBuilder = new RszFile.Builder(Repository, rszVersion) { Objects = Objects };
                    mainRszBuilder.AlignOffset = mainRszStart;
                    mainRsz = mainRszBuilder.Build();
                    mainRszEnd = mainRszStart + mainRsz.Data.Length;
                }
                else
                {
                    mainRszEnd = mainRszStart;
                }

                var secondaryRszStart = mainRszEnd + MiddleBytes.Length;
                var secondaryRszBuilder = new RszFile.Builder(Repository, rszVersion) { Objects = GraphObjects };
                secondaryRszBuilder.AlignOffset = secondaryRszStart;
                var secondaryRsz = secondaryRszBuilder.Build();
                var endOffset = secondaryRszStart + secondaryRsz.Data.Length;

                // Header slots: the first three are layout-specific (preserved for the spawner layout,
                // derived for the standard layout); the last four are section offsets.
                long slot0, slot1, slot2;
                if (Unknown == 0)
                {
                    slot0 = 0;
                    slot1 = NodeEntries.Length > 0 ? NodeSectionOffset : 0;
                    slot2 = 0;
                }
                else
                {
                    slot0 = HeaderSlots.Length > 0 ? HeaderSlots[0] : 0;
                    slot1 = HeaderSlots.Length > 1 ? HeaderSlots[1] : 0;
                    slot2 = HeaderSlots.Length > 2 ? HeaderSlots[2] : 0;
                }

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                bw.Write(Magic);
                bw.Write(Version);
                bw.Write(Hash);
                bw.Write(Unknown);
                bw.Write(Unknown == 0 ? NodeEntries.Length : NodeCount);
                bw.Write(GraphHash);
                bw.Write(0xCDCDCDCDu);
                bw.Write(slot0);
                bw.Write(slot1);
                bw.Write(slot2);
                bw.Write(emitMainRsz ? (long)mainRszStart : 0L);
                bw.Write(emitMainRsz ? (long)mainRszEnd : 0L);
                bw.Write((long)secondaryRszStart);
                bw.Write((long)endOffset);
                bw.Write(nodeSection);
                if (mainRsz != null)
                    bw.Write(mainRsz.Data.Span);
                bw.Write(MiddleBytes);
                bw.Write(secondaryRsz.Data.Span);

                return new PogFile(Version, ms.ToArray());
            }
        }
    }
}