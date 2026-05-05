using System;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class RszFolder : IRszSceneNode
    {
        public RszObjectNode Settings { get; }
        public ImmutableArray<IRszSceneNode> Children { get; } = [];

        public RszFolder(RszObjectNode settings, ImmutableArray<IRszSceneNode> children)
        {
            Settings = ValidateSettings(settings);
            Children = children;
        }


        public RszFolder Add(IRszSceneNode node) => WithChildren(Children.Add(node));

        public RszFolder WithSettings(RszObjectNode settings) => new RszFolder(settings, Children);

        public RszFolder WithChildren(ImmutableArray<IRszSceneNode> children)
        {
            return new RszFolder(Settings, children);
        }

        public string Name => ((RszStringNode)Settings[0]).Value;

        public RszFolder WithName(string name) => WithSettings(Settings.Set("Name", name));

        ImmutableArray<IRszNode> IRszNodeContainer.Children => Children.CastArray<IRszNode>();
        IRszSceneNode IRszSceneNode.WithChildren(ImmutableArray<IRszSceneNode> children) => WithChildren(children);
        IRszNodeContainer IRszNodeContainer.WithChildren(ImmutableArray<IRszNode> children) => WithChildren(children.Cast<IRszSceneNode>().ToImmutableArray());

        public override string ToString() => Name;

        private static RszObjectNode ValidateSettings(RszObjectNode settings)
        {
            if (settings?.Type.Name != "via.Folder")
            {
                throw new ArgumentException("Settings must be of type via.Folder.", nameof(settings));
            }

            return settings;
        }
    }
}
