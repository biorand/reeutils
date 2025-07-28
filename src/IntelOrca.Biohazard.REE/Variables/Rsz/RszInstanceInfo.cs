using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal class RszInstanceInfo
    {
        public uint TypeId { get; set; }
        public uint Crc { get; set; }
    }
}
