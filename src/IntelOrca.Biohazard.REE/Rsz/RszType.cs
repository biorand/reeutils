using System.Collections.Immutable;
using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}")]
    public class RszType(RszTypeRepository repository)
    {
        public RszTypeRepository Repository => repository;
        public uint Id { get; set; }
        public uint Crc { get; set; }
        public string Name { get; set; } = "";
        public ImmutableArray<RszTypeField> Fields { get; set; } = [];

        public string Namespace
        {
            get
            {
                var lastFullStop = Name.LastIndexOf('.');
                return lastFullStop == -1 ? "" : Name[..lastFullStop];
            }
        }

        public string NameWithoutNamespace
        {
            get
            {
                var lastFullStop = Name.LastIndexOf('.');
                return lastFullStop == -1 ? Name : Name[(lastFullStop + 1)..];
            }
        }

        public bool IsEnum
        {
            get
            {
                if (Fields.Length == 1)
                {
                    var field = Fields[0];
                    if (field.Name == "value__")
                        return true;
                }
                return false;
            }
        }

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
