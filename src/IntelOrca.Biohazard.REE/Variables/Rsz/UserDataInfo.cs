using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UserDataInfo
    {
        public uint Hash;
        public uint CRC;
        public ulong StringOffset;
    }
}
