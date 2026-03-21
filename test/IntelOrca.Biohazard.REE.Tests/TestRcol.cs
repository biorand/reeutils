using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRcol : IDisposable
    {
        private readonly OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Theory]
        [MemberData(nameof(RcolFiles))]
        public void Rebuild_Rcol_File(string gameName, string path)
        {
            AssertRebuild(gameName, path);
        }

        [Fact]
        public void Rebuild_RE7_DefaultBulletCollider()
        {
            AssertRebuild(GameNames.RE7, "natives/stm/collision/collider/weapon/defaultbullet.rcol.20");
        }

        [Fact]
        public void Rebuild_RE7_EM4100()
        {
            AssertRebuild(GameNames.RE7, "natives/stm/ch8/collision/collider/enemy/em4100/em4100.rcol.20");
        }

        public static IEnumerable<object[]> RcolFiles()
        {
            var pakList = PakList.FromFile(@"M:\git\biorand-re7\src\Biohazard.BioRand.RE7\data\pakcontentsrt.txt.gz");
            var rcols = pakList.Entries.Where(x => x.Contains(".rcol.")).Order().ToArray();
            foreach (var rcol in rcols)
            {
                yield return new object[] { GameNames.RE7, rcol };
            }
        }

        private void AssertRebuild(string gameName, string path)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var data = _pakHelper.GetFileData(gameName, path);
            if (data == null)
                return;

            var input = new RcolFile(FileVersion.FromPath(path), data);
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();

            File.WriteAllBytes(@"M:\temp\input.rcol.20", input.Data.Span);
            File.WriteAllBytes(@"M:\temp\output.rcol.20", output.Data.Span);

            var outputBuilder = output.ToBuilder(repo);

            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            Assert.Equal(input.Data.Length, output.Data.Length);
        }
    }
}
