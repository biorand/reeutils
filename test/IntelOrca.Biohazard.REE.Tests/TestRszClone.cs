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
