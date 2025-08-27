using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}: {Type}")]
    public sealed class RszTypeField
    {
        public string Name { get; set; } = "";
        public int Align { get; set; }
        public int Size { get; set; }
        public bool IsArray { get; set; }
        public RszFieldType Type { get; set; }
        public RszType? ObjectType { get; set; }
    }
}
