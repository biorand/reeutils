using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UserDataInfo
    {
        public int InstanceId;
        public uint Crc;
        public ulong PathOffset;
    }
}
