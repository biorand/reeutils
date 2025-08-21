using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszNullNode : IRszNode
    {
        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new NotSupportedException();
        }

        public override string ToString() => "NULL";
    }
}
