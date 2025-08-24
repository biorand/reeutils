﻿using System.Collections.Generic;
using System.Collections.Immutable;

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

        public ImmutableArray<RszType> GetNestedTypes(RszType type)
        {
            var result = ImmutableArray.CreateBuilder<RszType>();
            foreach (var t in _nameToTypeMap.Values)
            {
                if (t.Namespace == type.Name)
                {
                    result.Add(t);
                }
            }
            result.Sort(new System.Comparison<RszType>((a, b) => a.Name.CompareTo(b.Name)));
            return result.ToImmutable();
        }

        public void AddType(RszType type)
        {
            _idToTypeMap.Add(type.Id, type);
            _nameToTypeMap.Add(type.Name, type);
        }

        public RszStructNode Create(string name)
        {
            return FromName(name)!.Create();
        }
    }
}
