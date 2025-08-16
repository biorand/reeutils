using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestScn : IDisposable
    {
        private PatchedPakFile _pak;

        public TestScn()
        {
            _pak = GetVanillaPak();
        }

        public void Dispose()
        {
            _pak.Dispose();
        }

        [Fact]
        public void Rebuild_RE4_LEVEL_CP10_CHP1_1_010()
        {
            AssertRebuild("natives/stm/_chainsaw/leveldesign/chapter/cp10_chp1_1/level_cp10_chp1_1_010.scn.20");
        }

        private void AssertRebuild(string path)
        {
            var repo = new RszTypeRepository();
            var input = new ScnFile(20, _pak.GetFileData(path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            // Check our new file is same size as old one (should be for most cases)
            Assert.Equal(input.Data.Length, output.Data.Length);
        }

        private static PatchedPakFile GetVanillaPak()
        {
            var patch3 = Path.Combine(GetInstallPath(), "re_chunk_000.pak.patch_003.pak");
            return new PatchedPakFile(patch3);
        }

        private static string GetInstallPath() => @"G:\re4r\vanilla";
    }
}
