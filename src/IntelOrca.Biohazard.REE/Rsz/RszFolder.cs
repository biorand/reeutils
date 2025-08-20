using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszFolder : IRszSceneNode
    {
        private RszStructNode _settings;
        private ImmutableArray<IRszSceneNode> _children = [];

        public RszFolder(RszStructNode settings, ImmutableArray<IRszSceneNode> children)
        {
            _settings = settings;
            _children = children;
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

        public string Name => ((RszStringNode)_settings[0]).Value;

        public override string ToString() => Name;
    }
}
