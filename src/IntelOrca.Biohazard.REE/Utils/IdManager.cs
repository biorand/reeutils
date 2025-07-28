using System;
using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard.REE.Utils
{
    using System.Collections.Generic;

    public class IdManager
    {
        private static IdManager? _instance;
        public static IdManager Instance => _instance ??= new IdManager();

        private int _nextId = 1;
        private readonly Dictionary<int, int> _reasyToInstance = new();
        private readonly Dictionary<int, int> _instanceToReasy = new();

        private IdManager() { }

        public int RegisterInstance(int instanceId)
        {
            if (_instanceToReasy.ContainsKey(instanceId))
                return _instanceToReasy[instanceId];

            int reasyId = _nextId++;
            _reasyToInstance[reasyId] = instanceId;
            _instanceToReasy[instanceId] = reasyId;

            return reasyId;
        }

        public int ForceRegisterInstance(int instanceId, int reasyId)
        {
            if (_instanceToReasy.TryGetValue(instanceId, out int oldReasy))
            {
                _reasyToInstance.Remove(oldReasy);
            }

            if (_reasyToInstance.TryGetValue(reasyId, out int oldInstance))
            {
                _instanceToReasy.Remove(oldInstance);
            }

            _reasyToInstance[reasyId] = instanceId;
            _instanceToReasy[instanceId] = reasyId;

            if (reasyId >= _nextId)
                _nextId = reasyId + 1;

            return reasyId;
        }

        public int? GetInstanceId(int reasyId)
        {
            return _reasyToInstance.TryGetValue(reasyId, out int instanceId) ? instanceId : (int?)null;
        }

        public void UpdateInstanceId(int oldInstanceId, int newInstanceId)
        {
            if (!_instanceToReasy.TryGetValue(oldInstanceId, out int reasyId))
                return;

            _instanceToReasy.Remove(oldInstanceId);
            _instanceToReasy[newInstanceId] = reasyId;
            _reasyToInstance[reasyId] = newInstanceId;
        }

        public void RemoveInstance(int instanceId)
        {
            if (!_instanceToReasy.TryGetValue(instanceId, out int reasyId))
                return;

            _instanceToReasy.Remove(instanceId);
            _reasyToInstance.Remove(reasyId);
        }

        public void UpdateAllMappings(Dictionary<int, int> idMapping, HashSet<int>? deletedIds = null)
        {
            deletedIds ??= new HashSet<int>();
            Dictionary<int, int> newInstanceToReasy = new();

            foreach (int deletedId in deletedIds)
            {
                if (_instanceToReasy.TryGetValue(deletedId, out int reasyId))
                {
                    _reasyToInstance.Remove(reasyId);
                }
            }

            foreach (var pair in _instanceToReasy)
            {
                int oldId = pair.Key;
                int reasyId = pair.Value;

                if (deletedIds.Contains(oldId))
                    continue;

                if (idMapping.TryGetValue(oldId, out int newId))
                {
                    newInstanceToReasy[newId] = reasyId;
                    _reasyToInstance[reasyId] = newId;
                }
                else
                {
                    newInstanceToReasy[oldId] = reasyId;
                }
            }

            _instanceToReasy.Clear();
            foreach (var kvp in newInstanceToReasy)
                _instanceToReasy[kvp.Key] = kvp.Value;
        }

        public int GetReasyIdForInstance(int instanceId)
        {
            if (instanceId <= 0)
                return 0;

            return _instanceToReasy.TryGetValue(instanceId, out int reasyId)
                ? reasyId
                : RegisterInstance(instanceId);
        }
    }

}
