using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Rsz;
using ModelContextProtocol.Client;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    /// <summary>
    /// Tests the fix_re8_chapter1_inventory_unlock MCP tool against a synthetic file shaped like the
    /// real mainflowfsm/chapter1/c01_main.fsmv2.40 (root -> child named "0" carrying a single
    /// app.GameFlowNode action) -- no real game files needed.
    /// </summary>
    public sealed class TestFixModifier
    {
        [Fact]
        public async Task UnlockChapter1Inventory_Appends_ReleaseMenu_And_ChangeInventory_To_First_Node()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var temp = new TempFolder();
            var repo = McpEmbeddedData.GetRszTypeRepository("re8");

            var sourcePath = temp.GetSubPath("c01_main.fsmv2.40");
            File.WriteAllBytes(sourcePath, BuildSyntheticMainFlowFile(repo));
            var outputPath = temp.GetSubPath("c01_main_patched.fsmv2.40");

            await using var client = await McpServerTestHost.CreateClientAsync(cancellationToken);

            var toolNames = (await client.ListToolsAsync(cancellationToken: cancellationToken)).Select(x => x.Name).ToArray();
            Assert.Contains("fix_re8_chapter1_inventory_unlock", toolNames);

            await McpServerTestHost.CallToolTextAsync(client, "set_game", new Dictionary<string, object?>
            {
                ["game"] = "re8"
            }, cancellationToken);

            var resultText = await McpServerTestHost.CallToolTextAsync(client, "fix_re8_chapter1_inventory_unlock", new Dictionary<string, object?>
            {
                ["path"] = sourcePath,
                ["outputPath"] = outputPath
            }, cancellationToken);

            Assert.Contains("app.ReleaseMenuAction", resultText);
            Assert.Contains("app.ChangeInventoryAction", resultText);

            var patchedBytes = File.ReadAllBytes(outputPath);
            var tree = new BhvtFile(40, patchedBytes).ReadTree(repo);
            var firstNode = tree.Children.Single(c => c.Node.Name == "0").Node;

            Assert.Equal(3, firstNode.Actions.Length);
            Assert.Contains(firstNode.Actions, a => a.Instance.Type.Name == "app.GameFlowNode");
            Assert.Contains(firstNode.Actions, a => a.Instance.Type.Name == "app.ReleaseMenuAction");
            Assert.Contains(firstNode.Actions, a => a.Instance.Type.Name == "app.ChangeInventoryAction");

            // The two new actions must not collide with the pre-existing action's id.
            var ids = firstNode.Actions.Select(a => RszSerializer.Deserialize<uint>(a.Instance["v1_ID"])).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
        }

        private static byte[] BuildSyntheticMainFlowFile(RszTypeRepository repo)
        {
            var builder = new BhvtFile.Builder(repo, 40, 16);

            var gameFlowAction = repo.Create("app.GameFlowNode")
                .SetField("v0_Enabled", true)
                .SetField("v1_ID", 111u);

            var firstNode = new BhvtNode(
                new BhvtNodeId(1, 0), "0",
                BhvtNodeAttributes.IsEnabled | BhvtNodeAttributes.IsRestartable | BhvtNodeAttributes.IsFsmNode,
                0, false, false, BhvtWorkFlags.None, 0, 0, ImmutableArray<uint>.Empty,
                null, null, ImmutableArray<RszObjectNode>.Empty,
                [new BhvtAction(gameFlowAction, 0)],
                ImmutableArray<BhvtChild>.Empty, ImmutableArray<BhvtState>.Empty,
                ImmutableArray<BhvtTransition>.Empty, ImmutableArray<BhvtAllState>.Empty, null);

            builder.Root = builder.Root.WithChildren([new BhvtChild(firstNode, null)]);
            return builder.Build().Data.ToArray();
        }
    }
}
