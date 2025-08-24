using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public interface IRszSceneNode : IRszNode
    {
        new ImmutableArray<IRszSceneNode> Children { get; set; }

        IRszSceneNode WithChildren(ImmutableArray<IRszSceneNode> children);
    }
}
