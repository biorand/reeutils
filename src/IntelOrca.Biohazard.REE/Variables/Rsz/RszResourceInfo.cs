using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct RszResourceInfo
    {
        [FieldOffset(0)]
        public uint Reserved;

        // Only one of these will be used at a time
        [FieldOffset(0)]
        public string StringValue;

        [FieldOffset(4)]
        public uint StringOffset;
    }
}
