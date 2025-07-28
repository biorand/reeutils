using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Pfb
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PfbHeader
    {
        public uint Signature;
        public uint InfoCount;
        public uint ResourceCount;
        public uint GameObjectRefInfoCount;
        public uint UserdataCount;
        public uint Reserved;
        public ulong GameObjectRefInfoTbl;
        public ulong ResourceInfoTbl;
        public ulong UserdataInfoTbl;
        public ulong DataOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Pfb16Header
    {
        public uint Signature;
        public uint InfoCount;
        public uint ResourceCount;
        public uint GameObjectRefInfoCount;
        public ulong GameObjectRefInfoTbl;
        public ulong ResourceInfoTbl;
        public ulong DataOffset;
    }
}
