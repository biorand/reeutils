using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RszInstanceInfo
    {
        public uint TypeId;
        public uint Crc;
    }
}
