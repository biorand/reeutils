using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UserHeader
    {
        public uint Magic;
        public uint ResourceCount;
        public uint UserdataCount;
        public uint InfoCount;
        public ulong ResourceOffset;
        public ulong UserdataOffset;
        public ulong DataOffset;
        public ulong Reserved;
    }
}
