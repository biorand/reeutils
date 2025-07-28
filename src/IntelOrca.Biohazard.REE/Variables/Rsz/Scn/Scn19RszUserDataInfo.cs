using IntelOrca.Biohazard.REE.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard.REE.Variables.Rsz.Scn
{
    internal class Scn19RSZUserDataInfo
    {
        public const int Size = 24;
        
        public int InstanceId;
        public int TypeId;
        public int JsonPathHash;
        public int DataSize;
        public ulong RszOffset;

        // ???
        public byte[]? Data;
        public byte[]? OriginalData;
        public bool Modified;
        public string? Value;
        public object? ParentUserdataRui;

        // ???
        public EmbeddedIdManager? IdManager; // Optional, used for embedded RSZs

        // Embedded RSZ fields
        public EmbeddedRszHeader? EmbeddedRszHeader;
        public List<int> EmbeddedObjectTable = new List<int>();
        public List<EmbeddedInstanceInfo> EmbeddedInstanceInfos = new List<EmbeddedInstanceInfo>();
        public List<Scn19RSZUserDataInfo> EmbeddedUserdataInfos = new List<Scn19RSZUserDataInfo>();
        public Dictionary<int, Dictionary<string, object>> EmbeddedInstances = new Dictionary<int, Dictionary<string, object>>();

        public int Parse(byte[] data, int offset)
        {
            if (offset + Size > data.Length)
                throw new ArgumentException($"Truncated RSZUserData info at 0x{offset:X}");
            InstanceId = BitConverter.ToInt32(data, offset);
            TypeId = BitConverter.ToInt32(data, offset + 4);
            JsonPathHash = BitConverter.ToInt32(data, offset + 8);
            DataSize = BitConverter.ToInt32(data, offset + 12);
            RszOffset = BitConverter.ToUInt64(data, offset + 16);
            return offset + Size;
        }

        public void MarkModified() => Modified = true;
    }
}
