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

        public RszStructNode Create(string name)
        {
            return FromName(name)!.Create();
        }
    }
}
