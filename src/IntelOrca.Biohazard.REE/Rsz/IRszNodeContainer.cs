using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public interface IRszNodeContainer : IRszNode
    {
        ImmutableArray<IRszNode> Children { get; }

        IRszNodeContainer WithChildren(ImmutableArray<IRszNode> children);
    }
}
