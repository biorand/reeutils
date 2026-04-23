using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables
{
    internal struct UvarHeader
    {
        public uint Version;
        public uint Magic;
        public ulong StringsOffset;
        public ulong DataOffset;
        public ulong EmbedsInfoOffset;
        public ulong HashInfoOffset;
        public ulong UnknownHeaderValue;
        public uint UvarHash;
        public ushort VariableCount;
        public ushort EmbedCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UvarHeaderV2
    {
        public uint Version;
        public uint Magic;
        public ulong StringsOffset;
        public ulong DataOffset;
        public ulong EmbedsInfoOffset;
        public ulong HashInfoOffset;
        public ulong UnknownHeaderValue;
        public uint UvarHash;
        public ushort VariableCount;
        public ushort EmbedCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UvarHeaderV3
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
