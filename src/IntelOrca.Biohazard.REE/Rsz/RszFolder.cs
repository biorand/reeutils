using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszFolder : IRszSceneNode
    {
        private RszStructNode _settings;
        public ImmutableArray<IRszSceneNode> Children { get; } = [];

        public RszFolder(RszStructNode settings, ImmutableArray<IRszSceneNode> children)
        {
            _settings = settings;
            Children = children;
        }

        public RszStructNode Settings
        {
            get => _settings;
            set
            {
                if (value?.Type.Name != "via.Folder")
                {
                    throw new ArgumentException("Settings must be of type via.Folder.");
                }
                _settings = value;
            }
        }

        public RszFolder WithChildren(ImmutableArray<IRszSceneNode> children)
        {
            return new RszFolder(Settings, children);
        }

        public string Name => ((RszStringNode)_settings[0]).Value;

        ImmutableArray<IRszNode> IRszNodeContainer.Children => Children.CastArray<IRszNode>();
        IRszSceneNode IRszSceneNode.WithChildren(ImmutableArray<IRszSceneNode> children) => WithChildren(children);
        IRszNodeContainer IRszNodeContainer.WithChildren(ImmutableArray<IRszNode> children) => WithChildren(children.CastArray<IRszSceneNode>());

        public override string ToString() => Name;
    }
}
