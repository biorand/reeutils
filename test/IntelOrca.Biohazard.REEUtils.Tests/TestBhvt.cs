using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IntelOrca.Biohazard.REE.Rsz;
using IntelOrca.Biohazard.REEUtils.FileTypes;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public sealed class TestBhvt
    {
        private const int Version = 40;

        public static TheoryData<string> FixtureNames => new()
        {
            "playertutorialarea.fsmv2.40",
            "tempfsm_areainout_oneshoot_playeraction.fsmv2.40",
            "tutorialdeactivate.fsmv2.40",
            // Additional fixtures picked from a full RE8 install (2026 real .fsmv2.40 files, all of which
            // read/build/round-trip semantically identically) to cover structural features the first 3 don't:
            "fsm_movehold.fsmv2.40", // IsBranch node
            "prisonnpc.fsmv2.40", // IsBranch + IsEnd nodes
            "prisonnpcinsightcheck.fsmv2.40", // IsEnd node
            "c10_2_boss.fsmv2.40", // larger tree (15 nodes)
            "triggerinaction_em4490thinkordersetaction.fsmv2.40", // sub-UVar tree with populated variables
            "ch1301g33.fsmv2.40", // multiple sub-UVar trees with populated variables
        };

        [Theory]
        [MemberData(nameof(FixtureNames))]
        public void Read_Fsmv2_Fixture(string name)
        {
            var data = GetFixture(name);
            var file = new BhvtFile(Version, data);
            var tree = file.ReadTree(GetRepository());

            Assert.Equal("root", tree.Name);

            // Every action's raw id (its own RSZ instance field) is what nodes reference it by -- if
            // resolution silently picked the wrong instance this would still be non-null, so this mainly
            // guards against the disambiguation algorithm throwing/dropping actions outright.
            foreach (var node in Walk(tree))
            {
                foreach (var action in node.Actions)
                {
                    Assert.NotNull(action.Instance);
                }
            }
        }

        [Theory]
        [MemberData(nameof(FixtureNames))]
        public void Round_Trips_Fsmv2_Fixture_Semantically(string name)
        {
            var data = GetFixture(name);
            var repository = GetRepository();
            var file = new BhvtFile(Version, data);
            var originalTree = file.ReadTree(repository);

            var rebuilt = file.ToBuilder(repository).Build();
            var reparsedTree = new BhvtFile(Version, rebuilt.Data).ReadTree(repository);

            Assert.Equal(Dump(originalTree), Dump(reparsedTree));
        }

        [Theory]
        [MemberData(nameof(FixtureNames))]
        public void Fsmv2FileHandler_GetJson_Decodes_Rsz_Objects(string name)
        {
            var data = GetFixture(name);
            var handler = FileHandlerFactory.Default.Create($"test/{name}", data, GetRepository());

            using var json = handler.GetJson(TreeOptions.Root);
            var text = JsonSupport.ToJsonString(json);

            Assert.Contains("\"@type\": \"via.behaviortree.SelectorFSM\"", text);
            Assert.Contains("\"name\": \"root\"", text);
        }

        [Theory]
        [MemberData(nameof(FixtureNames))]
        public void Fsmv2FileHandler_Import_Round_Trips_Edits(string name)
        {
            var data = GetFixture(name);
            var repository = GetRepository();
            var handler = FileHandlerFactory.Default.Create($"test/{name}", data, repository);

            using var json = handler.GetJson(TreeOptions.Root);
            var edited = json.RootElement.GetRawText().Replace("\"root\"", "\"root_renamed\"");
            using var editedJson = System.Text.Json.JsonDocument.Parse(edited);

            var imported = handler.Import(editedJson);

            var importedHandler = FileHandlerFactory.Default.Create($"test/{name}", imported, repository);
            using var reexported = importedHandler.GetJson(TreeOptions.Root);
            Assert.Contains("\"name\": \"root_renamed\"", JsonSupport.ToJsonString(reexported));
        }

        private static IEnumerable<BhvtNode> Walk(BhvtNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var n in Walk(child.Node)) yield return n;
            }
        }

        private static string Dump(BhvtNode node)
        {
            static string ActionId(RszObjectNode obj) => obj.Children.Length > 1 && obj.Children[1] is RszValueNode v ? v.ToString() ?? "?" : "?";

            var sb = new System.Text.StringBuilder();
            void Visit(BhvtNode n)
            {
                sb.Append(n.Name).Append('|')
                  .Append(n.Selector?.Type.Name).Append('|')
                  .Append(string.Join(",", n.Actions.Select(a => $"{a.Instance.Type.Name}:{ActionId(a.Instance)}"))).Append('|')
                  .Append(string.Join(",", n.States.Select(s => $"{s.Target}->{s.Condition?.Type.Name}"))).Append('|')
                  .Append(string.Join(",", n.Transitions.Select(t => $"->{t.Start}"))).Append('|')
                  .Append(string.Join(",", n.AllStates.Select(s => $"->{s.Target}:{s.Condition?.Type.Name}"))).Append('\n');
                foreach (var c in n.Children) Visit(c.Node);
            }
            Visit(node);
            return sb.ToString();
        }

        private static RszTypeRepository GetRepository() => McpEmbeddedData.GetRszTypeRepository("re8");

        private static byte[] GetFixture(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream($"Fixtures.{name}")
                ?? throw new FileNotFoundException($"Embedded fixture '{name}' not found.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
