using System.Text;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Variables;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestUvar : IDisposable
    {
        private PatchedPakFile _pak;

        public TestUvar()
        {
            _pak = GetVanillaPak();
        }

        public void Dispose()
        {
            _pak.Dispose();
        }

        [Fact]
        public void Rebuild_RE4_GLOBALVARIABLES()
        {
            AssertRebuild("natives/stm/_authoring/appsystem/globalvariables/globalvariables.uvar.3");
        }

        private void AssertRebuild(string path)
        {
            var input = new UvarFile(_pak.GetFileData(path));
            var inputBuilder = input.ToBuilder();
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder();

            var oldSpan = input.Data.Span;
            var newSpan = output.Data.Span;
            var sb = new StringBuilder();
            for (var i = 0; i < oldSpan.Length; i++)
            {
                if (oldSpan[i] != newSpan[i])
                {
                    sb.AppendLine($"{i:X8}");
                }
            }

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
