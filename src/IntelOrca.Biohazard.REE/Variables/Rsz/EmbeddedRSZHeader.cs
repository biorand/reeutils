using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct EmbeddedRSZHeader
    {
        public uint Magic;
        public uint Version;
        public uint ObjectCount;
        public uint InstanceCount;
        public uint UserdataCount;
        public uint Reserved;
        public ulong InstanceOffset;
        public ulong DataOffset;
        public ulong UserdataOffset;
    }
}
