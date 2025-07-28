using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct EmbeddedInstanceInfo
    {
        public uint TypeId;
        public uint Crc;
    }
}
