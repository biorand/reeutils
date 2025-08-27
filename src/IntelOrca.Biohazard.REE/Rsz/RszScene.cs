using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszScene : IRszSceneNode
    {
        public ImmutableArray<IRszSceneNode> Children { get; } = [];

        public RszScene()
        {
        }

        public RszScene(ImmutableArray<IRszSceneNode> children)
        {
            Children = children;
        }

        public RszScene Add(IRszSceneNode node)
        {
            return new RszScene(Children.Add(node));
        }

        ImmutableArray<IRszNode> IRszNodeContainer.Children => Children.CastArray<IRszNode>();

        public RszScene WithChildren(ImmutableArray<IRszSceneNode> children) => new RszScene(children);
        IRszSceneNode IRszSceneNode.WithChildren(ImmutableArray<IRszSceneNode> children) => WithChildren(children);
        IRszNodeContainer IRszNodeContainer.WithChildren(ImmutableArray<IRszNode> children) => WithChildren(children.Cast<IRszSceneNode>().ToImmutableArray());
    }
}
