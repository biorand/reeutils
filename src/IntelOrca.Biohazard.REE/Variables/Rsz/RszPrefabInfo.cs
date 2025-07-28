using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal class RszPrefabInfo
    {
        public const int Size = 8;

        public uint StringOffset { get; set; }
        public uint ParentId { get; set; }
    }
}
