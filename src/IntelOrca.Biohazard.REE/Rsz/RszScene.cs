using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszScene : IRszSceneNode
    {
        private ImmutableArray<IRszSceneNode> _children = [];

        public RszScene()
        {
        }

        public RszScene(ImmutableArray<IRszSceneNode> children)
        {
            _children = children;
        }

        public RszScene Add(IRszSceneNode node)
        {
            return new RszScene(_children.Add(node));
        }

        public IRszSceneNode WithChildren(ImmutableArray<IRszSceneNode> children)
        {
            return new RszScene(children);
        }

        public ImmutableArray<IRszSceneNode> Children
        {
            get => _children;
            set => _children = value;
        }

        ImmutableArray<IRszNode> IRszNode.Children
        {
            get => _children.CastArray<IRszNode>();
            set => _children = value.CastArray<IRszSceneNode>();
        }

        IRszSceneNode IRszSceneNode.WithChildren(ImmutableArray<IRszSceneNode> children) => WithChildren(children);
    }
}
