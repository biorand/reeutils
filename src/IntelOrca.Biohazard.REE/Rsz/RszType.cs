using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}")]
    public class RszType
    {
        public RszTypeKind Kind { get; set; }

        public RszType? ElementType { get; set; }
        public int ArrayDimensions { get; set; }
        public bool IsArray => ArrayDimensions > 0;
        public int Size { get; set; }

        public uint Id { get; set; }
        public uint Crc { get; set; }
        public string Name { get; set; } = "";
        public ImmutableArray<RszTypeField> Fields { get; set; }

        public Type? ClrType { get; set; }
    }
}
