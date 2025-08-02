using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Messages
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct MsgHeaderA
    {
        public uint Version;
        public uint Magic;
        public ulong HeaderOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MsgHeaderB
    {
        public uint EntryCount;
        public uint AttributeCount;
        public uint LanguageCount;
        public uint Reserved;
        public ulong DataOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MsgHeaderC
    {
        public ulong UnkDataOffset;
        public ulong LangDataOffset;
        public ulong AttributeOffset;
        public ulong AttributeNameOffset;
    }
}
