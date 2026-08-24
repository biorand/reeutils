using System;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszGameObject : IRszSceneNode
    {
        public const short UnknownPadding = -1;

        public RszGameObject(Guid guid, string? prefab, RszObjectNode settings, ImmutableArray<RszObjectNode> components, ImmutableArray<RszGameObject> children)
        {
            Guid = guid;
            Prefab = prefab;
            Settings = ValidateSettings(settings);
            Components = components;
            Children = children;
        }

        public Guid Guid { get; }
        public string? Prefab { get; }
        public short Padding { get; }
        public RszObjectNode Settings { get; }
        public ImmutableArray<RszObjectNode> Components { get; }
        public ImmutableArray<RszGameObject> Children { get; }

        public RszGameObject(Guid guid, string? prefab, short padding, RszObjectNode settings, ImmutableArray<RszObjectNode> components, ImmutableArray<RszGameObject> children)
        {
            Guid = guid;
            Prefab = prefab;
            Padding = padding;
            Settings = ValidateSettings(settings);
            Components = components;
            Children = children;
        }

        public RszGameObject WithGuid(Guid guid) => new RszGameObject(guid, Prefab, Padding, Settings, Components, Children);
        public RszGameObject WithPrefab(string prefab) => new RszGameObject(Guid, prefab, Padding, Settings, Components, Children);

        public string Name => ((RszStringNode)Settings[0]).Value;

        public RszGameObject WithName(string name)
        {
            return WithSettings(Settings.Set("Name", name));
        }

        public RszObjectNode? FindComponent(string type)
        {
            return Components.FirstOrDefault(x => x.Type.Name == type);
        }

        public RszGameObject WithSettings(RszObjectNode settings)
        {
            return new RszGameObject(
                Guid,
                Prefab,
                Padding,
                settings,
                Components,
                Children);
        }

        public RszGameObject WithComponents(ImmutableArray<RszObjectNode> components)
        {
            return new RszGameObject(
                Guid,
                Prefab,
                Padding,
                Settings,
                components,
                Children);
        }

        public RszGameObject WithChildren(ImmutableArray<RszGameObject> children)
        {
            return new RszGameObject(
                Guid,
                Prefab,
                Padding,
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

        private RszObjectNode ValidateSettings(RszObjectNode settings)
        {
            if (settings?.Type.Name != "via.GameObject")
            {
                throw new ArgumentException("Settings must be of type via.GameObject.", nameof(settings));
            }

            return settings;
        }
    }
}
