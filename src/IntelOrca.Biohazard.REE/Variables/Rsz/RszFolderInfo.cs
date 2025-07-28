using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RszFolderInfo
    {
        public int Id;
        public int ParentId;
    }
}
