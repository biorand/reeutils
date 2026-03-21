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
