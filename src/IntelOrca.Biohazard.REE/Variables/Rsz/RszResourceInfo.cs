using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal class RszResourceInfo
    {
        public uint StringOffset { get; set; }
        public uint Reserved { get; set; }
    }
}
