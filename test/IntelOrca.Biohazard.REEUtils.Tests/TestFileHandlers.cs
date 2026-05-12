using System;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using IntelOrca.Biohazard.REEUtils.Tools;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public sealed class TestFileHandlers
    {
        [Fact]
        public void FileHandlerFactory_Appends_Default_Versions_For_Pak_Paths()
        {
            Assert.Equal(
                "natives/stm/leveldesign/test.user.2",
                FileHandlerFactory.Default.GetFullPathFromArg("leveldesign/test.user"));
            Assert.Equal(
                "natives/stm/leveldesign/test.scn.20",
                FileHandlerFactory.Default.GetFullPathFromArg("leveldesign/test.scn"));
            Assert.Equal(
                "natives/stm/leveldesign/test.pfb.17",
                FileHandlerFactory.Default.GetFullPathFromArg("leveldesign/test.pfb"));
        }

        [Fact]
        public void RszJsonSerializer_RoundTrips_GameObject_Metadata()
        {
            var repo = GetRepository();
            var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var scene = new RszScene().Add(new RszGameObject(
                guid,
                "prefabs/test",
                CreateGameObjectSettings(repo, "SceneRoot"),
                [],
                []));

            var json = RszJsonSerializer.Serialize(scene, JsonSupport.CreateOptions());
            Assert.Contains("\"@guid\": \"11111111-2222-3333-4444-555555555555\"", json);
            Assert.Contains("\"Name\": \"SceneRoot\"", json);

            using var document = JsonDocument.Parse(json);
            var roundTripped = Assert.IsType<RszScene>(RszJsonSerializer.Deserialize(document, repo));
            var gameObject = Assert.IsType<RszGameObject>(Assert.Single(roundTripped.Children));
            Assert.Equal(guid, gameObject.Guid);
            Assert.Equal("prefabs/test", gameObject.Prefab);
            Assert.Equal("SceneRoot", gameObject.Name);
        }

        [Fact]
        public void UserFileHandler_RoundTrips_Json()
        {
            var repo = GetRepository();
            var original = CreateGameObjectSettings(repo, "UserRoot");
            using var input = JsonDocument.Parse(RszJsonSerializer.Serialize(original, JsonSupport.CreateOptions()));

            var importHandler = FileHandlerFactory.Default.Create("test.user.2", Array.Empty<byte>(), repo);
            var bytes = importHandler.Import(input);

            var exportHandler = FileHandlerFactory.Default.Create("test.user.2", bytes, repo);
            using var json = exportHandler.GetJson(TreeOptions.Root);
            var text = JsonSupport.ToJsonString(json);

            Assert.Contains("\"@type\": \"via.GameObject\"", text);
            Assert.Contains("\"Name\": \"UserRoot\"", text);
        }

        [Fact]
        public void PakRead_Returns_Structured_User_And_Scene_Json()
        {
            using var temp = new TempFolder();
            var pakPath = temp.GetSubPath("test.pak");
            var repo = GetRepository();
            var sceneGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

            var builder = new PakFileBuilder();
            builder.AddEntry("natives/stm/test/userdata/test.user.2", BuildUserFile(repo, "UserMcp"));
            builder.AddEntry("natives/stm/test/scene/test.scn.20", BuildSceneFile(repo, "SceneMcp", sceneGuid));
            builder.Save(pakPath);

            using var session = new McpSession();
            session.SetGame("re9");
            session.OpenPak(pakPath);

            var userJson = PakTools.Read("natives/stm/test/userdata/test.user.2", session);
            Assert.Contains("\"@type\": \"via.GameObject\"", userJson);
            Assert.Contains("\"Name\": \"UserMcp\"", userJson);

            var sceneJson = PakTools.Read("natives/stm/test/scene/test.scn.20", session);
            Assert.Contains("\"@guid\": \"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", sceneJson);
            Assert.Contains("\"Name\": \"SceneMcp\"", sceneJson);
        }

        private static RszTypeRepository GetRepository() => McpEmbeddedData.GetRszTypeRepository("re9");

        private static RszObjectNode CreateGameObjectSettings(RszTypeRepository repo, string name)
        {
            return repo.Create("via.GameObject").Set("Name", name);
        }

        private static byte[] BuildUserFile(RszTypeRepository repo, string name)
        {
            var builder = new UserFile(EmbeddedData.GetFile("empty.user.2")!).ToBuilder(repo);
            builder.Objects = [CreateGameObjectSettings(repo, name)];
            return builder.Build().Data.ToArray();
        }

        private static byte[] BuildSceneFile(RszTypeRepository repo, string name, Guid guid)
        {
            var builder = new ScnFile(20, EmbeddedData.GetFile("empty.scn.20")!).ToBuilder(repo);
            builder.Scene = new RszScene().Add(new RszGameObject(guid, null, CreateGameObjectSettings(repo, name), [], []));
            return builder.Build().Data.ToArray();
        }
    }
}
