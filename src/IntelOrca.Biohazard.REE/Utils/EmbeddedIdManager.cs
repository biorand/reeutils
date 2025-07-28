using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard.REE.Utils
{
    public class EmbeddedIdManager
    {
        private readonly int _domainId;
        private int _nextId = 1;
        private readonly Dictionary<int, int> _reasyToInstance = new();
        private readonly Dictionary<int, int> _instanceToReasy = new();

        public EmbeddedIdManager(int domainId)
        {
            _domainId = domainId;
        }

        public int RegisterInstance(int instanceId)
        {
            if (_instanceToReasy.ContainsKey(instanceId))
                return _instanceToReasy[instanceId];

            int reasyId = _nextId++;
            _reasyToInstance[reasyId] = instanceId;
            _instanceToReasy[instanceId] = reasyId;

            return reasyId;
        }
        public void Reset()
        {
            _nextId = 1;
            _reasyToInstance.Clear();
            _instanceToReasy.Clear();
        }
    }

}
