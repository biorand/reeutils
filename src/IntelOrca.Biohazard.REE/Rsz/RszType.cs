using System.Collections.Immutable;
using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}")]
    public class RszType
    {
        public uint Id { get; set; }
        public uint Crc { get; set; }
        public string Name { get; set; } = "";
        public ImmutableArray<RszTypeField> Fields { get; set; } = [];
    }
}
