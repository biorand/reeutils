using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public interface IRszSceneNode : IRszNodeContainer
    {
        new ImmutableArray<IRszSceneNode> Children { get; }

        IRszSceneNode WithChildren(ImmutableArray<IRszSceneNode> children);
    }
}
