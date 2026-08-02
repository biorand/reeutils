using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>A prefab game-object reference. Only populated in .fsmv2 files, never in plain .bhvt trees.</summary>
    public sealed class BhvtGameObjectReference(Guid guid, ImmutableArray<int> values)
    {
        public Guid Guid { get; } = guid;
        public ImmutableArray<int> Values { get; } = values;

        public override string ToString() => Guid.ToString();
    }
}
