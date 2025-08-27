using System;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszGameObject : IRszSceneNode
    {
        private RszObjectNode _settings;

        public RszGameObject(Guid guid, string? prefab, RszObjectNode settings, ImmutableArray<RszObjectNode> components, ImmutableArray<RszGameObject> children)
        {
            Guid = guid;
            Prefab = prefab;
            _settings = settings;
            Components = components;
            Children = children;
        }

        public Guid Guid { get; set; }
        public string? Prefab { get; set; }

        public RszObjectNode Settings
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

        public ImmutableArray<RszObjectNode> Components { get; set; }

        public ImmutableArray<RszGameObject> Children { get; set; }

        public RszGameObject WithGuid(Guid guid) => new RszGameObject(guid, Prefab, Settings, Components, Children);
        public RszGameObject WithPrefab(string prefab) => new RszGameObject(Guid, prefab, Settings, Components, Children);

        public string Name => ((RszStringNode)_settings[0]).Value;

        public RszGameObject WithName(string name)
        {
            return WithSettings(_settings.Set("Name", name));
        }

        public RszObjectNode? FindComponent(string type)
        {
            return Components.FirstOrDefault(x => x.Type.Name == type);
        }

        public RszGameObject WithSettings(RszObjectNode settings)
        {
            if (settings?.Type.Name != "via.GameObject")
            {
                throw new ArgumentException("Settings must be of type via.GameObject.");
            }
            return new RszGameObject(
                Guid,
                Prefab,
                settings,
                Components,
                Children);
        }

        public RszGameObject WithComponents(ImmutableArray<RszObjectNode> components)
        {
            return new RszGameObject(
                Guid,
                Prefab,
                Settings,
                components,
                Children);
        }

        public RszGameObject WithChildren(ImmutableArray<RszGameObject> children)
        {
            return new RszGameObject(
                Guid,
                Prefab,
                Settings,
                Components,
                children);
        }

        public RszGameObject AddOrUpdateComponent(RszObjectNode component)
        {
            for (var i = 0; i < Components.Length; i++)
            {
                if (Components[i].Type == component.Type)
                {
                    return WithComponents(Components.SetItem(i, component));
                }
            }
            return WithComponents(Components.Add(component));
        }

        public RszGameObject AddOrUpdateChild(RszGameObject gameObject)
        {
            for (var i = 0; i < Children.Length; i++)
            {
                if (Children[i].Guid == gameObject.Guid)
                {
                    return WithChildren(Children.SetItem(i, gameObject));
                }
            }
            return WithChildren(Children.Add(gameObject));
        }

        ImmutableArray<IRszSceneNode> IRszSceneNode.Children => Children.CastArray<IRszSceneNode>();
        ImmutableArray<IRszNode> IRszNodeContainer.Children => Children.CastArray<IRszNode>();

        IRszSceneNode IRszSceneNode.WithChildren(ImmutableArray<IRszSceneNode> children) => WithChildren(children.Cast<RszGameObject>().ToImmutableArray());
        IRszNodeContainer IRszNodeContainer.WithChildren(ImmutableArray<IRszNode> children) => WithChildren(children.Cast<RszGameObject>().ToImmutableArray());

        public override string ToString() => Name;
    }
}
