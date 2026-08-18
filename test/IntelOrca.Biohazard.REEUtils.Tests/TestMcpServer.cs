using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public sealed class TestMcpServer
    {
        [Fact]
        public void SetGame_Clears_Open_Pak_When_Switching_Games()
        {
            using var temp = new TempFolder();
            var pakPath = temp.GetSubPath("test.pak");

            var builder = new PakFileBuilder();
            builder.AddEntry("natives/stm/leveldesign/test.txt", Encoding.UTF8.GetBytes("hello"));
            builder.Save(pakPath);

            using var session = new McpSession();
            session.OpenPak(pakPath);
            session.SetGame("re8");

            Assert.NotNull(session.Pak);

            session.SetGame("re9");

            Assert.Null(session.Pak);
            Assert.Equal("re9", session.Game);
            Assert.NotNull(session.PakList);
            Assert.NotNull(session.RszTypeRepository);
        }

        [Fact]
        public async Task Mcp_Server_Supports_Basic_Workflow()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var temp = new TempFolder();
            var pakPath = temp.GetSubPath("test.pak");
            var pakListPath = temp.GetSubPath("paklist.txt");

            const string textPath = "natives/stm/leveldesign/chapter/test/test.txt";
            const string msgPath = "natives/stm/message/test.msg.22";
            const string scenePath = "natives/stm/test/scene/test.scn.20";
            var repo = McpEmbeddedData.GetRszTypeRepository("re9");

            var pakBuilder = new PakFileBuilder();
            pakBuilder.AddEntry(textPath, Encoding.UTF8.GetBytes("This contains LevelFlow text."));
            pakBuilder.AddEntry(msgPath, BuildMsg("Greeting", "Hello MCP"));
            pakBuilder.AddEntry(scenePath, BuildSceneFile(repo));
            pakBuilder.Save(pakPath);

            File.WriteAllText(pakListPath, string.Join('\n', new[] { textPath, msgPath, scenePath }) + "\n");

            await using var client = await CreateClientAsync(cancellationToken);

            var toolNames = (await client.ListToolsAsync(cancellationToken: cancellationToken)).Select(x => x.Name).ToArray();
            Assert.Contains("open_pak", toolNames);
            Assert.Contains("open_pak_list", toolNames);
            Assert.Contains("list_games", toolNames);
            Assert.Contains("search", toolNames);
            Assert.Contains("find", toolNames);
            Assert.Contains("read", toolNames);
            Assert.Contains("set_game", toolNames);
            Assert.Contains("get_type", toolNames);
            Assert.Contains("generate_class", toolNames);

            var listGamesText = await CallToolTextAsync(client, "list_games", cancellationToken: cancellationToken);
            Assert.Contains("\"re9\"", listGamesText);

            var openPakText = await CallToolTextAsync(client, "open_pak", new Dictionary<string, object?>
            {
                ["path"] = pakPath
            }, cancellationToken);
            Assert.Contains(pakPath.Replace("\\", "\\\\"), openPakText);

            var findError = await client.CallToolAsync("find", new Dictionary<string, object?>
            {
                ["patterns"] = new[] { "*.txt" }
            }, cancellationToken: cancellationToken);
            Assert.True(findError.IsError is true);
            Assert.Contains("No pak list is loaded", GetText(findError));

            await CallToolTextAsync(client, "open_pak_list", new Dictionary<string, object?>
            {
                ["path"] = pakListPath
            }, cancellationToken);

            var listFilesText = await CallToolTextAsync(client, "list_files", new Dictionary<string, object?>
            {
                ["path"] = "natives/stm"
            }, cancellationToken);
            Assert.Contains("\"name\": \"leveldesign\"", listFilesText);
            Assert.Contains("\"name\": \"message\"", listFilesText);

            var searchText = await CallToolTextAsync(client, "search", new Dictionary<string, object?>
            {
                ["regex"] = "LevelFlow",
                ["paths"] = new[] { "natives/stm/leveldesign" },
                ["maxResults"] = 10
            }, cancellationToken);
            Assert.Contains(textPath, searchText);
            Assert.Contains("LevelFlow", searchText);

            var readText = await CallToolTextAsync(client, "read", new Dictionary<string, object?>
            {
                ["path"] = msgPath
            }, cancellationToken);
            Assert.Contains("\"name\": \"Greeting\"", readText);
            Assert.Contains("Hello MCP", readText);

            await CallToolTextAsync(client, "set_game", new Dictionary<string, object?>
            {
                ["game"] = "re9"
            }, cancellationToken);

            var collapsedSceneText = await CallToolTextAsync(client, "read", new Dictionary<string, object?>
            {
                ["path"] = scenePath
            }, cancellationToken);
            Assert.Contains("\"@type\": \"via.Transform\"", collapsedSceneText);

            var expandedSceneText = await CallToolTextAsync(client, "read", new Dictionary<string, object?>
            {
                ["path"] = scenePath,
                ["expand_nodes"] = new[] { "Root" }
            }, cancellationToken);

            using var collapsedSceneJson = JsonDocument.Parse(collapsedSceneText);
            using var expandedSceneJson = JsonDocument.Parse(expandedSceneText);

            var collapsedRootComponent = GetFirstComponent(collapsedSceneJson.RootElement);
            var collapsedChildComponent = GetFirstChildComponent(collapsedSceneJson.RootElement);
            var expandedRootComponent = GetFirstComponent(expandedSceneJson.RootElement);
            var expandedChildComponent = GetFirstChildComponent(expandedSceneJson.RootElement);

            Assert.Single(collapsedRootComponent.EnumerateObject());
            Assert.Single(collapsedChildComponent.EnumerateObject());
            Assert.True(expandedRootComponent.EnumerateObject().Count() > 1);
            Assert.True(expandedChildComponent.EnumerateObject().Count() > 1);

            var getTypeText = await CallToolTextAsync(client, "get_type", new Dictionary<string, object?>
            {
                ["typeName"] = "app.InventorySlotCapacitySetting"
            }, cancellationToken);
            Assert.Contains("InventorySlotCapacitySetting", getTypeText);
            Assert.Contains("\"fields\"", getTypeText);

            var generateClassText = await CallToolTextAsync(client, "generate_class", new Dictionary<string, object?>
            {
                ["typeNames"] = new[] { "app.InventorySlotCapacitySetting" },
                ["includeEnums"] = false
            }, cancellationToken);
            Assert.Contains("InventorySlotCapacitySetting", generateClassText);
        }

        [Fact]
        public async Task Mcp_Find_Returns_Matching_Paths()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            using var temp = new TempFolder();
            var pakPath = temp.GetSubPath("test.pak");
            var pakListPath = temp.GetSubPath("paklist.txt");

            const string textPath = "natives/stm/leveldesign/chapter/test/test.txt";
            const string msgPath = "natives/stm/message/test.msg.22";
            const string scenePath = "natives/stm/test/scene/test.scn.20";

            var pakBuilder = new PakFileBuilder();
            pakBuilder.AddEntry(textPath, Encoding.UTF8.GetBytes("hello"));
            pakBuilder.AddEntry(msgPath, Encoding.UTF8.GetBytes("hello"));
            pakBuilder.AddEntry(scenePath, Encoding.UTF8.GetBytes("hello"));
            pakBuilder.Save(pakPath);

            File.WriteAllText(pakListPath, string.Join('\n', new[] { textPath, msgPath, scenePath }) + "\n");

            await using var client = await CreateClientAsync(cancellationToken);
            await CallToolTextAsync(client, "open_pak", new Dictionary<string, object?>
            {
                ["path"] = pakPath
            }, cancellationToken);
            await CallToolTextAsync(client, "open_pak_list", new Dictionary<string, object?>
            {
                ["path"] = pakListPath
            }, cancellationToken);

            var findText = await CallToolTextAsync(client, "find", new Dictionary<string, object?>
            {
                ["patterns"] = new[] { "test.scn", "*.txt" }
            }, cancellationToken);

            using var findJson = JsonDocument.Parse(findText);
            var paths = findJson.RootElement.GetProperty("paths").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(new[] { textPath, scenePath }, paths);

            var noMatchText = await CallToolTextAsync(client, "find", new Dictionary<string, object?>
            {
                ["patterns"] = new[] { "does-not-exist" }
            }, cancellationToken);

            using var noMatchJson = JsonDocument.Parse(noMatchText);
            Assert.Empty(noMatchJson.RootElement.GetProperty("paths").EnumerateArray());
        }

        private static async Task<McpClient> CreateClientAsync(CancellationToken cancellationToken)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var reeutilsDll = Path.Combine(repoRoot, "src", "reeutils", "bin", configuration, "net10.0", "reeutils.dll");

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "reeutils",
                Command = "dotnet",
                Arguments = [reeutilsDll, "mcp"],
                WorkingDirectory = repoRoot,
            });

            return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        }

        private static async Task<string> CallToolTextAsync(
            McpClient client,
            string toolName,
            IReadOnlyDictionary<string, object?>? arguments = null,
            CancellationToken cancellationToken = default)
        {
            var result = await client.CallToolAsync(toolName, arguments ?? new Dictionary<string, object?>(), cancellationToken: cancellationToken);
            Assert.False(result.IsError is true, GetText(result));
            return GetText(result);
        }

        private static string GetText(CallToolResult result)
        {
            return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
        }

        private static byte[] BuildMsg(string name, string text)
        {
            var builder = new MsgFile.Builder
            {
                Version = 22,
                Languages = [LanguageId.English],
                Messages =
                [
                    new Msg
                    {
                        Guid = Guid.NewGuid(),
                        Crc = MurMur3.HashData(name),
                        Name = name,
                        Values = [new MsgValue(LanguageId.English, text)]
                    }
                ]
            };
            return builder.Build().Data.ToArray();
        }

        private static byte[] BuildSceneFile(IntelOrca.Biohazard.REE.Rsz.RszTypeRepository repo)
        {
            var scene = new IntelOrca.Biohazard.REE.Rsz.RszScene().Add(new IntelOrca.Biohazard.REE.Rsz.RszGameObject(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                null,
                repo.Create("via.GameObject").Set("Name", "Root"),
                [repo.Create("via.Transform")],
                [new IntelOrca.Biohazard.REE.Rsz.RszGameObject(
                    Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
                    null,
                    repo.Create("via.GameObject").Set("Name", "Child"),
                    [repo.Create("via.Transform")],
                    [])]));

            var builder = new IntelOrca.Biohazard.REE.Rsz.ScnFile(20, EmbeddedData.GetFile("empty.scn.20")!).ToBuilder(repo);
            builder.Scene = scene;
            return builder.Build().Data.ToArray();
        }

        private static JsonElement GetFirstComponent(JsonElement root)
        {
            var gameObject = Assert.Single(root.GetProperty("@children").EnumerateArray());
            return Assert.Single(gameObject.GetProperty("@components").EnumerateArray());
        }

        private static JsonElement GetFirstChildComponent(JsonElement root)
        {
            var gameObject = Assert.Single(root.GetProperty("@children").EnumerateArray());
            var childObject = Assert.Single(gameObject.GetProperty("@children").EnumerateArray());
            return Assert.Single(childObject.GetProperty("@components").EnumerateArray());
        }
    }
}
