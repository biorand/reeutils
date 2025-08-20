using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszGameObject : IRszSceneNode
    {
        private RszStructNode _settings;

        public RszGameObject(Guid guid, string? prefab, RszStructNode settings, ImmutableArray<IRszNode> components, ImmutableArray<RszGameObject> children)
        {
            Guid = guid;
            Prefab = prefab;
            _settings = settings;
            Components = components;
            Children = children;
        }

        public Guid Guid { get; set; }
        public string? Prefab { get; set; }

        public RszStructNode Settings
        {
            get => _settings;
            set
            {
                if (value?.Type.Name != "via.GameObject")
                {
                    throw new ArgumentException("Settings must be of type via.GameObject.");
                }
                _settings = value;
            }
        }

        public ImmutableArray<IRszNode> Components { get; set; }

        public ImmutableArray<RszGameObject> Children { get; set; }

        ImmutableArray<IRszSceneNode> IRszSceneNode.Children
        {
            get => Children.CastArray<IRszSceneNode>();
            set => Children = value.CastArray<RszGameObject>();
        }

        ImmutableArray<IRszNode> IRszNode.Children
        {
            get => Children.CastArray<IRszNode>();
            set => Children = value.CastArray<RszGameObject>();
        }

        public string Name => ((RszStringNode)_settings[0]).Value;

        public override string ToString() => Name;
    }
}
