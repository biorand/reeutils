using System.Text.Json;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestScn : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Rebuild_RE2_ST4_701_0_GIMMICK()
        {
            AssertRebuild(GameNames.RE2, "natives/x64/objectroot/scene/location/rpd/level_100/environments/st4_701_0/gimmick.scn.19", 31432);
        }

        [Fact]
        public void Rebuild_RE4_LEVEL_CP10_CHP1_1_010()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/leveldesign/chapter/cp10_chp1_1/level_cp10_chp1_1_010.scn.20");
        }

        [Fact]
        public void Rebuild_RE4_LEVEL_LOC40_900()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/leveldesign/location/loc40/level_loc40_900.scn.20");
        }

        [Fact]
        public void Rebuild_RE8_C02_1_C02_1_FARM()
        {
            AssertRebuild(GameNames.RE8, "natives/stm/spawn/enemy/scene/c02_1/c02_1_c02_1_farm.scn.20");
        }

        [Fact]
        public void Rebuild_RE8_ST03_EVENTPROPS()
        {
            AssertRebuild(GameNames.RE8, "natives/stm/spawn/item/scene/st03/st03_eventprops.scn.20");
        }

        [Fact]
        public void Rebuild_RE8_C02_2_ENEMYSET_MADAM()
        {
            AssertRebuild(GameNames.RE8, "natives/stm/spawn/enemy/scene/c02_2/c02_2_enemyset_madam.scn.20");
        }

        [Fact]
        public void Rebuild_RE8_C02_4_ENEMYSET_BOSS()
        {
            AssertRebuild(GameNames.RE8, "natives/stm/Spawn/Enemy/scene/c02_4/c02_4_enemyset_boss.scn.20");
        }

        private void AssertRebuild(string gameName, string path, int? expectedLength = null)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            if (input.Data.Length == output.Data.Length)
            {
                Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            }
            Assert.Equal(expectedLength ?? input.Data.Length, output.Data.Length);
        }

        private void AssertRebuildInstanceCount(string gameName, string path, int? expectedLength = null)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            Assert.Equal(input.InstanceCount, output.InstanceCount);
        }

        private void AssertRebuildScene(string gameName, string path, int? expectedLength = null)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            var inputJson = RszJsonSerializer.Serialize(inputBuilder.Scene, new JsonSerializerOptions()
            {
                WriteIndented = true
            });
            var outputJson = RszJsonSerializer.Serialize(outputBuilder.Scene, new JsonSerializerOptions()
            {
                WriteIndented = true
            });
            Assert.Equal(inputJson, outputJson);
        }
    }
}
