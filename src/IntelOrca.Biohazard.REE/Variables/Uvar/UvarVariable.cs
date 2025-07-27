using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Uvar
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UvarVariable
    {
        public Guid Guid;
        public ulong NameOffset;
        public ulong FloatOffset;
        public ulong UknOffset;
        public uint TypeValNumBits;
        public uint NameHash;

        public int TypeVal
        {
            readonly get => (int)(TypeValNumBits & 0xFFFFFF);
            set => TypeValNumBits = TypeValNumBits & 0xFF000000 | (uint)value & 0xFFFFFF;
        }

        public int NumBits
        {
            readonly get => (int)(TypeValNumBits >> 24 & 0xFF);
            set => TypeValNumBits = TypeValNumBits & 0x00FFFFFF | (uint)(value & 0xFF) << 24;
        }
    }
}
