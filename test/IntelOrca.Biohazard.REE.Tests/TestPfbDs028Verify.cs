using System.Collections.Generic;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestPfbDs028Verify : IDisposable
    {
        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;
        private readonly ITestOutputHelper _output;
        public TestPfbDs028Verify(ITestOutputHelper output) => _output = output;
        public void Dispose() => _pakHelper.Dispose();

        [Fact]
        public void VerifyNoOrphans()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE9);
            var path = "natives/stm/gameassets/detailsearch/prefab/detail/ds028_detail.pfb.18";
            var data = _pakHelper.GetFileData(GameNames.RE9, path);
            var original = new PfbFile(FileVersion.FromPath(path), data);
            var rebuilt = original.ToBuilder(repo).Build();

            // Stability: rebuilding the rebuilt file must be byte-identical.
            var rebuilt2 = rebuilt.ToBuilder(repo).Build();
            Assert.True(rebuilt.Data.Span.SequenceEqual(rebuilt2.Data.Span), "Rebuilt file must round-trip stably.");

            // Reachability: every instance (except the null instance at index 0) must be reachable
            // from the root object list via Object/UserData references. An unreachable instance is
            // an orphan the engine cannot resolve.
            var instances = rebuilt.Rsz.ReadInstanceList(repo);
            var nodeToId = new Dictionary<IRszNode, int>();
            for (var i = 0; i < instances.Length; i++)
                nodeToId[instances[i].Value] = i;

            var reachable = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (var id in rebuilt.Rsz.ReadObjectInstanceIndices())
            {
                if (reachable.Add(id))
                    queue.Enqueue(id);
            }

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                CollectObjectIds(instances[id].Value, nodeToId, reachable, queue);
            }

            var unreachable = new List<int>();
            for (var i = 1; i < instances.Length; i++)
            {
                if (!reachable.Contains(i))
                    unreachable.Add(i);
            }

            _output.WriteLine($"InstanceCount={instances.Length} ObjectList={rebuilt.Rsz.ReadObjectInstanceIndices().Length} Reachable={reachable.Count} Unreachable={unreachable.Count}");
            foreach (var u in unreachable)
                _output.WriteLine($"  UNREACHABLE [{u}] {instances[u].Value}");
            Assert.Empty(unreachable);
        }

        private static void CollectObjectIds(IRszNode node, Dictionary<IRszNode, int> nodeToId, HashSet<int> reachable, Queue<int> queue)
        {
            if (node is IRszNodeContainer container)
            {
                if (nodeToId.TryGetValue(node, out var id) && reachable.Add(id))
                    queue.Enqueue(id);
                foreach (var child in container.Children)
                    CollectObjectIds(child, nodeToId, reachable, queue);
            }
            else if (nodeToId.TryGetValue(node, out var id))
            {
                // Leaf instance (e.g. user data) referenced by a field of this scene graph.
                reachable.Add(id);
            }
        }
    }
}
