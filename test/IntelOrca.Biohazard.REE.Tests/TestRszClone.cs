using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRszClone : IDisposable
    {
        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Clone_UsesProvidedGuidCallback()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE9);
            var path = "natives/stm/gameassets/character/scene/chap1_01/chap1_01_weaponpool.scn.21";
            var scene = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.RE9, path))
                .ToBuilder(repo)
                .Scene;

            // Record original game object guids
            var originalGoGuids = new HashSet<Guid>();
            scene.VisitGameObjects(go => originalGoGuids.Add(go.Guid));
            Assert.NotEmpty(originalGoGuids);

            // Deterministic callback mapping each old guid to a new, unique guid
            var map = new Dictionary<Guid, Guid>();
            var counter = 0;
            Guid NewGuid(Guid old)
            {
                if (!map.TryGetValue(old, out var newGuid))
                {
                    newGuid = new Guid(counter++, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    map[old] = newGuid;
                }
                return newGuid;
            }

            // Clone each top-level game object
            var clonedChildren = scene
                .Children
                .Select(c => c is RszGameObject go ? (IRszSceneNode)go.Clone(NewGuid) : c)
                .ToImmutableArray();
            var clonedScene = scene.WithChildren(clonedChildren);

            // Verify all game object guids are new and map-derived
            var clonedGoGuids = new HashSet<Guid>();
            clonedScene.VisitGameObjects(go => clonedGoGuids.Add(go.Guid));
            Assert.Equal(originalGoGuids.Count, clonedGoGuids.Count);
            Assert.Equal(map.Values.ToHashSet(), clonedGoGuids);
            Assert.Empty(originalGoGuids.Intersect(clonedGoGuids));

            // Verify GameObjectRef remapping follows the callback-derived map
            var originalRefs = CollectGameObjectRefs(scene);
            var clonedRefs = CollectGameObjectRefs(clonedScene);
            Assert.Equal(originalRefs.Count, clonedRefs.Count);
            for (var i = 0; i < originalRefs.Count; i++)
            {
                if (map.TryGetValue(originalRefs[i], out var expected))
                {
                    Assert.Equal(expected, clonedRefs[i]);
                }
                else
                {
                    // References to objects outside the cloned set are left untouched
                    Assert.Equal(originalRefs[i], clonedRefs[i]);
                }
            }

            // Verify original scene is unmutated
            var originalAfter = new HashSet<Guid>();
            scene.VisitGameObjects(go => originalAfter.Add(go.Guid));
            Assert.Equal(originalGoGuids, originalAfter);
        }

        /// <summary>
        /// A cloned game object must serialize to its own instances. RE2 (non-RT) scenes share
        /// instances by node identity on rebuild, and Clone only replaces nodes it changes, so a
        /// clone used to end up pointing at the same component instances as its source, which
        /// crashes the game.
        /// </summary>
        [Fact]
        public void Clone_RE2_RebuildDoesNotShareInstancesWithOriginal()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE2);
            var path = "natives/x64/objectroot/scene/scenario/scenariono/rpd/enemy/s02_0100.scn.19";
            var input = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.RE2, path));
            var builder = input.ToBuilder(repo);

            // Baseline: the vanilla scene has no shared nodes.
            Assert.Empty(FindSharedObjectNodes(builder.Scene));

            RszGameObject? source = null;
            builder.Scene.VisitGameObjects(go =>
            {
                if (source == null && go.Components.Length >= 3)
                    source = go;
            });
            Assert.NotNull(source);

            var clone = source.Clone();
            builder.Scene = builder.Scene.VisitGameObjects(go =>
                go.Guid == source.Guid ? go.WithChildren(go.Children.Add(clone)) : go);

            var output = builder.Build();
            var reread = output.ToBuilder(repo).Scene;

            Assert.Empty(FindSharedObjectNodes(reread));
        }

        private static List<string> FindSharedObjectNodes(RszScene scene)
        {
            var seen = new HashSet<IRszNode>(ReferenceEqualityComparer.Instance);
            var shared = new List<string>();
            foreach (var child in scene.Children)
                Walk(child);
            return shared;

            // Own traversal: RszExtensions.Visit walks a game object's children twice and never
            // reaches its settings node.
            void Walk(IRszNode node)
            {
                switch (node)
                {
                    case RszFolder folder:
                        foreach (var child in folder.Children)
                            Walk(child);
                        break;
                    case RszGameObject gameObject:
                        WalkObject(gameObject.Settings);
                        foreach (var component in gameObject.Components)
                            WalkObject(component);
                        foreach (var child in gameObject.Children)
                            Walk(child);
                        break;
                }
            }

            void WalkObject(IRszNode node)
            {
                if (node is RszObjectNode objectNode)
                {
                    if (!seen.Add(objectNode))
                    {
                        shared.Add(objectNode.Type.Name);
                        return;
                    }
                }
                if (node is IRszNodeContainer container)
                {
                    foreach (var child in container.Children)
                        WalkObject(child);
                }
            }
        }

        private static List<Guid> CollectGameObjectRefs(RszScene scene)
        {
            var refs = new List<Guid>();
            scene.Visit(node =>
            {
                if (node is RszValueNode valueNode && valueNode.Type == RszFieldType.GameObjectRef)
                {
                    refs.Add(RszSerializer.Deserialize<Guid>(valueNode));
                }
                return node;
            });
            return refs;
        }
    }
}
