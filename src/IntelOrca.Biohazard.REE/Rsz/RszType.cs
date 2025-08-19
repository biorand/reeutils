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

        public int FindFieldIndex(string name)
        {
            for (var i = 0; i < Fields.Length; i++)
            {
                if (Fields[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
