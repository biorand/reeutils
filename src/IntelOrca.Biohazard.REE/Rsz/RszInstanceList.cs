using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal sealed class RszInstanceList
    {
        private readonly ImmutableArray<RszInstance> _instances = [];

        public int Count => _instances.Length;

        public RszInstanceList(ImmutableArray<RszInstance> instances)
        {
            _instances = instances;
        }

        public RszInstance GetDeepHierarchy(RszInstanceId id)
        {
            return GetDeepDereference(_instances[id.Index]);
        }

        private RszInstance GetDeepDereference(RszInstance instance)
        {
            if (instance.Value is RszInstance[] children)
            {
                for (var i = 0; i < children.Length; i++)
                {
                    children[i] = GetDeepDereference(children[i]);
                }
            }
            else if (instance.Value is RszInstanceId[] references)
            {
                var dereferenced = new RszInstance[references.Length];
                for (var i = 0; i < references.Length; i++)
                {
                    dereferenced[i] = _instances[references[i].Index];
                }
                instance = new RszInstance(instance.Id, instance.Type, dereferenced);
            }
            return instance;
        }
    }
}
