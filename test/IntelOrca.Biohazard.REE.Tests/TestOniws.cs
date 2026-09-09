using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    /// <summary>
    /// Tests for Onimusha: Way of the Sword (oniws) support. The embedded RSZ
    /// repository and pak list are vendored with the tool, so repository parsing
    /// is always available. Corpus round-trip tests require the retail game pak
    /// and skip when it is not installed.
    /// </summary>
    public sealed class TestOniws : IDisposable
    {
        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void TypeRepository_Contains_App_PlayerManager()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            Assert.NotNull(repo.FromName("app.PlayerManager"));
        }

        [Fact]
        public void TypeRepository_Contains_Via_GameObject()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            Assert.NotNull(repo.FromName("via.GameObject"));
        }

        [Fact]
        public void Rebuild_Oniws_User()
        {
            AssertRebuildUser("natives/STM/GameDesign/System/Achievement/AchievementCountData.user.3");
        }

        [Fact]
        public void Rebuild_Oniws_User_With_Resources_Is_ByteIdentical()
        {
            // Resource-bearing .user files carry a wrapper resource table before the RSZ stream;
            // rebuilding must preserve it or the file is corrupted (game fails to load).
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            var path = "natives/STM/Sound/UserData/12_Bank/Resident/FSM/MainMission/Ms000055_BankListData.user.3";
            var input = new UserFile(_pakHelper.GetFileData(GameNames.ONIWS, path));
            Assert.Equal(1, input.ResourceCount);
            var output = input.ToBuilder(repo).Build();
            Assert.True(output.Data.Span.SequenceEqual(input.Data.Span),
                "Resource-bearing .user rebuild must be byte-identical.");
        }

        [Fact]
        public void Rebuild_Oniws_Scene()
        {
            AssertRebuildScene("natives/STM/GameDesign/System/Environment/Stage/Stage100/Area/Area.scn.21");
        }

                [Fact]
                        public void Rebuild_Oniws_Scene_Json_RoundTrip()
                        {
                            // The Area scene carries raw byte-blob (RszFieldType.Data) members; the JSON
                            // export->import path must round-trip them as base64. Regression test for
                            // "Unsupported RSZ value type 'Data'" on oniws scene import.
                            var path = "natives/STM/GameDesign/System/Environment/Stage/Stage100/Area/Area.scn.21";
                            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
                            var input = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.ONIWS, path));
                            var scene = input.ReadScene(repo);

                            var json = RszJsonSerializer.Serialize(scene, repo);
                            var rebuiltScene = (RszScene)RszJsonSerializer.Deserialize(json, repo);

                            // JSON -> scene -> JSON is idempotent: the export faithfully captures the whole tree.
                            var json2 = RszJsonSerializer.Serialize(rebuiltScene, repo);
                            Assert.Equal(json, json2);

                            var builder = new ScnFile.Builder(repo, input.Version, input.RszVersion);
                            builder.Resources.AddRange(input.Resources);
                            builder.Prefabs.AddRange(input.Prefabs);
                            builder.Scene = rebuiltScene;
                            var output = builder.Build();

                            // The JSON-imported scene rebuilds to the exact same binary as the native
                            // binary->builder->binary path; both may differ from the original in padding
                            // layout (pre-existing scn v16 rebuild behavior, see AssertRebuildScene).
                            var binOutput = input.ToBuilder(repo).Build();
                            Assert.True(output.Data.Span.SequenceEqual(binOutput.Data.Span));
                            Assert.Equal(input.InstanceCount, output.InstanceCount);
                            Assert.Equal(16, input.RszVersion);
                        }

        [Fact]
        public void Rebuild_Oniws_Prefab()
        {
            AssertRebuildPrefab("natives/STM/GameDesign/System/Prefab/FadeCreator.pfb.18");
        }

        [Fact]
        public void Rebuild_Oniws_Message()
        {
            AssertRebuildMessage("natives/STM/Ace/Data/GUI/ACE_SAVE_MSG.msg.23");
        }

        private void AssertRebuildUser(string path)
                {
                    var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
                    var input = new UserFile(_pakHelper.GetFileData(GameNames.ONIWS, path));
                    var inputBuilder = input.ToBuilder(repo);
                    var output = inputBuilder.Build();
                    var outputBuilder = output.ToBuilder(repo);

                    Assert.Equal(16, input.RszVersion);
                    Assert.Equal(input.RszVersion, output.RszVersion);
                    Assert.Equal(inputBuilder.Objects.Length, outputBuilder.Objects.Length);
                    Assert.Equal(input.InstanceCount, output.InstanceCount);
                }

                private void AssertRebuildScene(string path)
                {
                    var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
                    var input = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.ONIWS, path));
                    var inputBuilder = input.ToBuilder(repo);
                    var output = inputBuilder.Build();
                    var outputBuilder = output.ToBuilder(repo);

                    Assert.Equal(16, input.RszVersion);
                    Assert.Equal(input.RszVersion, output.RszVersion);
                    Assert.Equal(input.InstanceCount, output.InstanceCount);
                    if (input.Data.Length == output.Data.Length)
                    {
                        Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
                    }
                }

                private void AssertRebuildPrefab(string path)
                {
                    var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
                    var input = new PfbFile(FileVersion.FromPath(path), _pakHelper.GetFileData(GameNames.ONIWS, path));
                    var inputBuilder = input.ToBuilder(repo);
                    var output = inputBuilder.Build();
                    var outputBuilder = output.ToBuilder(repo);

                    Assert.Equal(16, input.RszVersion);
                    Assert.Equal(input.RszVersion, output.RszVersion);
                    Assert.Equal(input.InstanceCount, output.InstanceCount);
                    Assert.Equal(inputBuilder.OrphanObjects.Count, outputBuilder.OrphanObjects.Count);
                }

                private void AssertRebuildMessage(string path)
                {
                    var input = new MsgFile(_pakHelper.GetFileData(GameNames.ONIWS, path));
                    var inputBuilder = input.ToBuilder();
                    var output = inputBuilder.Build();
                    var outputBuilder = output.ToBuilder();

                    Assert.Equal(23, inputBuilder.Version);
                    Assert.Equal(inputBuilder.Version, outputBuilder.Version);
                    Assert.Equal(inputBuilder.Languages, outputBuilder.Languages);
                    Assert.Equal(inputBuilder.Messages.Count, outputBuilder.Messages.Count);
                    for (var i = 0; i < inputBuilder.Messages.Count; i++)
                    {
                        Assert.Equal(inputBuilder.Messages[i].Guid, outputBuilder.Messages[i].Guid);
                        Assert.Equal(inputBuilder.Messages[i].Crc, outputBuilder.Messages[i].Crc);
                        Assert.Equal(inputBuilder.Messages[i].Name, outputBuilder.Messages[i].Name);
                        Assert.Equal(inputBuilder.Messages[i].Values.Count, outputBuilder.Messages[i].Values.Count);
                        Assert.Equal(inputBuilder.Messages[i].Attributes.Count, outputBuilder.Messages[i].Attributes.Count);
                    }
                }
            }
        }