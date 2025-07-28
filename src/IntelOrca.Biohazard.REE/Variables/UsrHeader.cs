using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct UsrHeader
    {
        public readonly uint Signature;
        public readonly uint ResourceCount;
        public readonly uint UserdataCount;
        public readonly uint InfoCount;
        public readonly ulong ResourceInfoTbl;
        public readonly ulong UserdataInfoTbl;
        public readonly ulong DataOffset;
        public readonly ulong Reserved;
    }
}
