using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// BHVT container header. Field layout shifts at two independent version thresholds: an unknown
    /// 4-byte pad was inserted after the hash at version 42 (RE9/Pragmata-era), and userdataPathsOffset
    /// was inserted at version 34 (RE8 and later). The format itself stores no version of its own --
    /// it's only known from the .fsmv2.NN filename suffix, same as .pfb/.scn version thresholds.
    /// </summary>
    internal readonly struct BhvtHeader(int version, ReadOnlyMemory<byte> data)
    {
        public int Version => version;

        private bool HasV42Pad => Version >= 42;
        private bool HasUserdataPaths => Version >= 34;

        private int OffsetsStart => 8 + (HasV42Pad ? 4 : 0);
        private int UserdataPathsPos => OffsetsStart + 14 * 8;
        private int VariableOffsetPos => UserdataPathsPos + (HasUserdataPaths ? 8 : 0);

        public int Size => VariableOffsetPos + 24;

        public uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(0, 4));
        public uint Hash => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(4, 4));

        public long NodeOffset => ReadOffset(0);
        public long ActionOffset => ReadOffset(1);
        public long SelectorOffset => ReadOffset(2);
        public long SelectorCallerOffset => ReadOffset(3);
        public long ConditionsOffset => ReadOffset(4);
        public long TransitionEventOffset => ReadOffset(5);
        public long ExpressionTreeConditionsOffset => ReadOffset(6);
        public long StaticActionOffset => ReadOffset(7);
        public long StaticSelectorCallerOffset => ReadOffset(8);
        public long StaticConditionsOffset => ReadOffset(9);
        public long StaticTransitionEventOffset => ReadOffset(10);
        public long StaticExpressionTreeConditionsOffset => ReadOffset(11);
        public long StringOffset => ReadOffset(12);
        public long ResourcePathsOffset => ReadOffset(13);

        /// <summary>0 (absent) below version 34.</summary>
        public long UserdataPathsOffset => HasUserdataPaths
            ? BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(UserdataPathsPos, 8))
            : 0;

        public long VariableOffset => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(VariableOffsetPos, 8));
        public long BaseVariableOffset => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(VariableOffsetPos + 8, 8));
        public long ReferencePrefabGameObjectsOffset => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(VariableOffsetPos + 16, 8));

        private long ReadOffset(int index) => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(OffsetsStart + index * 8, 8));
    }
}
