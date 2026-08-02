using System.Collections.Immutable;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Rsz;
using IntelOrca.Biohazard.REEUtils.FileTypes;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    /// <summary>
    /// Tests Fsmv2FileHandler's JSON export/import wiring against a small, synthetically-built tree --
    /// no real game files needed. Exhaustive coverage of BhvtFile itself (the actual parser/builder,
    /// against every real RE8 .fsmv2.40 file) lives in IntelOrca.Biohazard.REE.Tests.TestBhvt instead.
    /// </summary>
    public sealed class TestBhvt
    {
        [Fact]
        public void Fsmv2FileHandler_GetJson_Decodes_Synthetic_Tree()
        {
            var repo = GetRepository();
            var file = BuildSyntheticFile(repo, out _);

            var handler = FileHandlerFactory.Default.Create("test.fsmv2.40", file, repo);
            using var json = handler.GetJson(TreeOptions.Root);
            var text = JsonSupport.ToJsonString(json);

            Assert.Contains("\"name\": \"root\"", text);
            Assert.Contains("\"name\": \"Child\"", text);
            Assert.Contains("\"@type\": \"app.ActivateCommonObject\"", text);
            Assert.Contains("12345", text);
        }

        [Fact]
        public void Fsmv2FileHandler_Import_Round_Trips_Edits()
        {
            var repo = GetRepository();
            var file = BuildSyntheticFile(repo, out _);
            var handler = FileHandlerFactory.Default.Create("test.fsmv2.40", file, repo);

            using var json = handler.GetJson(TreeOptions.Root);
            var edited = json.RootElement.GetRawText().Replace("\"Child\"", "\"ChildRenamed\"");
            using var editedJson = JsonDocument.Parse(edited);

            var imported = handler.Import(editedJson);

            var importedHandler = FileHandlerFactory.Default.Create("test.fsmv2.40", imported, repo);
            using var reexported = importedHandler.GetJson(TreeOptions.Root);
            var text = JsonSupport.ToJsonString(reexported);

            Assert.Contains("\"name\": \"ChildRenamed\"", text);
            Assert.Contains("\"@type\": \"app.ActivateCommonObject\"", text);
        }

        private static RszTypeRepository GetRepository() => McpEmbeddedData.GetRszTypeRepository("re8");

        private static byte[] BuildSyntheticFile(RszTypeRepository repo, out BhvtNode child)
        {
            var builder = new BhvtFile.Builder(repo, 40, 16);

            var action = repo.Create("app.ActivateCommonObject").SetField("v1_ID", 12345u);
            child = new BhvtNode(
                new BhvtNodeId(1, 0), "Child",
                BhvtNodeAttributes.IsEnabled | BhvtNodeAttributes.IsRestartable | BhvtNodeAttributes.IsFsmNode,
                0, false, false, BhvtWorkFlags.None, 0, 0, ImmutableArray<uint>.Empty,
                null, null, ImmutableArray<RszObjectNode>.Empty,
                [new BhvtAction(action, 0)],
                ImmutableArray<BhvtChild>.Empty, ImmutableArray<BhvtState>.Empty,
                ImmutableArray<BhvtTransition>.Empty, ImmutableArray<BhvtAllState>.Empty, null);

            builder.Root = builder.Root.WithChildren([new BhvtChild(child, null)]);
            return builder.Build().Data.ToArray();
        }
    }
}
