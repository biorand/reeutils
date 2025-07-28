using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Pfb
{
    internal class PfbHeader
    {
        public const int Size = 56;

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

        public PfbHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for PfbHeader.");

            Signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            InfoCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            ResourceCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            GameObjectRefInfoCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
            UserdataCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
            Reserved = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
            GameObjectRefInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(24, 8));
            ResourceInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(32, 8));
            UserdataInfoTbl = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(40, 8));
            DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(48, 8));
        }

        internal class Pfb16Header
        {
            public const int Size = 40;

            public byte[] Signature = new byte[4];
            public uint InfoCount;
            public uint ResourceCount;
            public uint GameObjectRefInfoCount;
            public ulong GameObjectRefInfoTbl;
            public ulong ResourceInfoTbl;
            public ulong DataOffset;

            public void Parse(byte[] data)
            {
                if (data == null || data.Length < Size)
                    throw new ArgumentException($"Insufficient data for Pfb16Header: expected {Size} bytes, got {data?.Length ?? 0}");

                Array.Copy(data, 0, Signature, 0, 4);
                InfoCount = BitConverter.ToUInt32(data, 4);
                ResourceCount = BitConverter.ToUInt32(data, 8);
                GameObjectRefInfoCount = BitConverter.ToUInt32(data, 12);
                GameObjectRefInfoTbl = BitConverter.ToUInt64(data, 16);
                ResourceInfoTbl = BitConverter.ToUInt64(data, 24);
                DataOffset = BitConverter.ToUInt64(data, 32);
            }
        }

        internal class Pfb16ResourceInfo
        {
            /// <summary>
            /// Special ResourceInfo class for PFB.16 that contains direct string data.
            /// </summary>
            public string StringValue { get; set; } = string.Empty;
            public uint Reserved { get; set; } = 0;

            /// <summary>
            /// Compatibility property that emulates string_offset behavior.
            /// Returns a non-zero value if we have a string, 0 otherwise.
            /// </summary>
            public uint StringOffset
            {
                get => string.IsNullOrEmpty(StringValue) ? 0u : 1u;
                set { /* Accept setting for compatibility but ignore the value */ }
            }
        }
    }
}
