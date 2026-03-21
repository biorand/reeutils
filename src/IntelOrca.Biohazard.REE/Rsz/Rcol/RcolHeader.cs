using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Rsz.Rcol
{
    // Adapted from https://github.com/kagenocookie/RE-Engine-Lib/blob/master/REE-Lib/RszFile/RcolFile.cs.
    internal readonly struct RcolHeader(int version, ReadOnlyMemory<byte> data)
    {
        private readonly ReadOnlyMemory<byte> _data = data;

        public int Version { get; } = version;

        private ReadOnlySpan<byte> Span => _data.Span;

        public uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(0, 4));

        public int NumGroups =>
            Version == 2
                ? Span[4]
                : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(4, 4));

        public int NumShapes =>
            Version == 2
                ? Span[5]
                : Version >= 25
                    ? 0
                    : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(8, 4));

        public int NumUserData =>
            Version >= 25
                ? BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(8, 4))
                : 0;

        public int UknCount =>
            Version == 2
                ? BinaryPrimitives.ReadInt16LittleEndian(Span.Slice(6, 2))
                : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(Version >= 25 ? 12 : 12, 4));

        public int NumRequestSets =>
            Version == 2
                ? BinaryPrimitives.ReadInt16LittleEndian(Span.Slice(8, 2))
                : BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(Version >= 25 ? 16 : 16, 4));

        public uint MaxRequestSetId =>
            Version == 2
                ? BinaryPrimitives.ReadUInt16LittleEndian(Span.Slice(10, 2))
                : BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(Version >= 25 ? 20 : 20, 4));

        private int OffsetAfterHeaderCounts => Version == 2 ? 12 : 24;

        private int OffsetAfterOptionalCounts =>
            Version > 11
                ? OffsetAfterHeaderCounts + 8
                : OffsetAfterHeaderCounts;

        public int NumIgnoreTags =>
            Version > 11
                ? BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterHeaderCounts, 4))
                : 0;

        public int NumAutoGenerateJoints =>
            Version > 11
                ? BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterHeaderCounts + 4, 4))
                : 0;

        public int UserDataSize =>
            BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterOptionalCounts, 4));

        public int Status =>
            BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(OffsetAfterOptionalCounts + 4, 4));

        private int OffsetAfterStatus =>
            OffsetAfterOptionalCounts + 8 + (Version == 2 ? 4 : 0);

        public ulong UknRe3_A =>
            Version == 11
                ? BinaryPrimitives.ReadUInt64LittleEndian(Span.Slice(OffsetAfterStatus, 8))
                : 0;

        public ulong UknRe3_B =>
            Version == 11
                ? BinaryPrimitives.ReadUInt64LittleEndian(Span.Slice(OffsetAfterStatus + 8, 8))
                : 0;

        private int OffsetAfterRe3 =>
            Version == 11
                ? OffsetAfterStatus + 16
                : OffsetAfterStatus;

        public uint Ukn1 =>
            Version >= 20
                ? BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(OffsetAfterRe3, 4))
                : 0;

        public uint Ukn2 =>
            Version >= 20
                ? BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(OffsetAfterRe3 + 4, 4))
                : 0;

        private int OffsetAfterUkn12 =>
            Version >= 20
                ? OffsetAfterRe3 + 8
                : OffsetAfterRe3;

        public long GroupsPtrOffset =>
            BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12, 8));

        public long DataOffset =>
            BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 8, 8));

        public long RequestSetOffset =>
            BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 16, 8));

        public long IgnoreTagOffset =>
            Version > 11
                ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 24, 8))
                : 0;

        public long AutoGenerateJointDescOffset =>
            Version > 11
                ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 32, 8))
                : 0;

        public long RequestSetIDLookupsOffset =>
            Version == 2
                ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 24, 8))
                : 0;

        public ulong UknRe3 =>
            Version == 11
                ? BinaryPrimitives.ReadUInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + 24, 8))
                : 0;

        public long UnknPtr0 =>
            Version >= 20
                ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + (Version > 11 ? 40 : 24), 8))
                : 0;

        public long UnknPtr1 =>
            Version >= 20
                ? BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(OffsetAfterUkn12 + (Version > 11 ? 48 : 32), 8))
                : 0;

        public static int GetSize(int version) => version switch
        {
            2 => 56, // RE7
            11 => -1, // TODO: RE3R
            20 => 0x70, // RE7RT
            >= 25 => 90, // RE4R (and later?)
            _ => throw new ArgumentException($"Invalid version {version}!")
        };
    }
}
