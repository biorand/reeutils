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
        public void Rebuild_RE4_LEVEL_CP10_CHP1_1_010()
        {
            AssertRebuild("natives/stm/_chainsaw/leveldesign/chapter/cp10_chp1_1/level_cp10_chp1_1_010.scn.20");
        }

        private void AssertRebuild(string path)
        {
            var repo = GetTypeRepository();
            var input = new ScnFile(20, _pakHelper.GetFileData(GameNames.RE4, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            // Check our new file is same size as old one
            // Unfortunately the original file has random alignment, so we are a bit off
            Assert.Equal(71164, output.Data.Length);
        }

        private static RszTypeRepository GetTypeRepository()
        {
            var jsonPath = @"G:\apps\reasy\rszre4_reasy.json";
            var json = File.ReadAllBytes(jsonPath);
            var repo = RszRepositorySerializer.Default.FromJson(json);
            return repo;
        }
    }
}
