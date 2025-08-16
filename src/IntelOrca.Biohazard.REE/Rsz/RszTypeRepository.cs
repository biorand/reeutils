using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszTypeRepository
    {
        private Dictionary<uint, RszType> _idToTypeMap = [];
        private Dictionary<string, RszType> _nameToTypeMap = [];

        public RszType? FromId(uint id)
        {
            _idToTypeMap.TryGetValue(id, out var result);
            return result;
        }

        public RszType? FromName(string name)
        {
            _nameToTypeMap.TryGetValue(name, out var result);
            return result;
        }

        public void AddType(RszType type)
        {
            _idToTypeMap.Add(type.Id, type);
            _nameToTypeMap.Add(type.Name, type);
        }

        private Type? FindNativeClrType(string name)
        {
            return name switch
            {
                "System.Byte" => typeof(byte),
                "System.Int16" => typeof(short),
                "System.Int32" => typeof(int),
                "System.Int64" => typeof(long),
                "System.SByte" => typeof(sbyte),
                "System.String" => typeof(string),
                "System.UInt16" => typeof(ushort),
                "System.UInt32" => typeof(uint),
                "System.UInt64" => typeof(ulong),
                _ => null
            };
        }
    }
}
