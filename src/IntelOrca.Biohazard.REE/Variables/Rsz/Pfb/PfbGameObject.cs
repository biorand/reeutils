using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Pfb
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PfbGameObject
    {
        public int Id;
        public int ParentId;
        public int ComponentCount;
    }
}
