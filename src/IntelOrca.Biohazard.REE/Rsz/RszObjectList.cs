using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// RSZ data deserialized into a list of RSZ instances.
    /// </summary>
    internal sealed class RszObjectList
    {
        private readonly ImmutableArray<RszInstance> _instances;

        public RszObjectList(ImmutableArray<RszInstance> instances)
        {
            _instances = instances;
        }

        public int Count => _instances.Length;
        public RszInstance this[int index] => _instances[index];
    }
}
