using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Uvar
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UvarHeader
    {
        public uint Version;
        public uint Magic;
        public ulong StringsOffset;
        public ulong DataOffset;
        public ulong EmbedsInfoOffset;
        public ulong HashInfoOffset;
        public uint UvarHash;
        public ushort VariableCount;
        public ushort EmbedCount;
    }
}
