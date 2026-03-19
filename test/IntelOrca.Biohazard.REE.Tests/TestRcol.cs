using IntelOrca.Biohazard.REE.Rsz.Rcol;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestRcol : IDisposable
    {
        private readonly OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Rebuild_RE7_DefaultBulletCollider()
        {
            AssertRebuild(GameNames.RE7, "natives/stm/collision/collider/weapon/defaultbullet.rcol.20");
        }

        private void AssertRebuild(string gameName, string path)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new RcolFile(FileVersion.FromPath(path), _pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            Assert.Equal(input.Data.Length, output.Data.Length);
        }
    }
}
