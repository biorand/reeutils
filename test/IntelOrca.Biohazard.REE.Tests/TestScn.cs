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
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/leveldesign/chapter/cp10_chp1_1/level_cp10_chp1_1_010.scn.20", 71148);
        }

        [Fact]
        public void Rebuild_RE4_LEVEL_LOC40_900()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/leveldesign/location/loc40/level_loc40_900.scn.20", 128);
        }

        private void AssertRebuild(string gameName, string path, int expectedLength)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new ScnFile(FileVersion.FromPath(path), _pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            // We currently don't keep prefabs that have no owner, so file size might be different
            if (input.Data.Length == output.Data.Length)
            {
                Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            }
            Assert.Equal(expectedLength, output.Data.Length);
        }
    }
}
