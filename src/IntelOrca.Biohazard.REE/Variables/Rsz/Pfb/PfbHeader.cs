using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Variables.Pfb
{
    internal class PfbHeader
    {
        public uint Signature;
        public uint InfoCount;
        public uint ResourceCount;
        public uint GameObjectRefInfoCount;
        public uint? UserdataCount; // Optional for Pfb16
        public uint? Reserved; // Optional for Pfb16
        public ulong GameObjectRefInfoTbl;
        public ulong ResourceInfoTbl;
        public ulong? UserdataInfoTbl; // Optional for Pfb16
        public ulong DataOffset;
        public int Version { get; private set; }

        public void Parse(byte[] data, int version)
        {
            Version = version;

            if (data.Length < (Version == 16 ? 40 : 56))
                throw new ArgumentException($"Invalid PFB header data: expected at least {(Version == 16 ? 40 : 56)} bytes, got {data.Length}");

            Signature = BitConverter.ToUInt32(data, 0);
            InfoCount = BitConverter.ToUInt32(data, 4);
            ResourceCount = BitConverter.ToUInt32(data, 8);
            GameObjectRefInfoCount = BitConverter.ToUInt32(data, 12);
            GameObjectRefInfoTbl = BitConverter.ToUInt64(data, 16);
            ResourceInfoTbl = BitConverter.ToUInt64(data, 24);

            if (Version == 16)
            {
                DataOffset = BitConverter.ToUInt64(data, 32);
            }
            else
            {
                UserdataCount = BitConverter.ToUInt32(data, 16);
                Reserved = BitConverter.ToUInt32(data, 20);
                UserdataInfoTbl = BitConverter.ToUInt64(data, 32);
                DataOffset = BitConverter.ToUInt64(data, 40);
            }
        }
    }
}
