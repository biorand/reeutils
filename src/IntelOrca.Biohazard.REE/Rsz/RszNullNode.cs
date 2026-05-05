using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszNullNode : IRszNode
    {
        public ImmutableArray<IRszNode> Children => [];

        public override string ToString() => "NULL";
    }
}
