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
    }
}
