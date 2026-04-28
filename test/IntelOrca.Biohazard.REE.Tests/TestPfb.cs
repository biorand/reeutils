using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestPfb : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Rebuild_RE4_WP4100()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/appsystem/prefab/weapon/wp4100.pfb.17", 5136);
        }

        [Fact]
        public void Rebuild_RE4_WP4202()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/appsystem/prefab/weapon/wp4202.pfb.17", 12148);
        }

        [Fact]
        public void Rebuild_RE4_ARROWSHELL()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/appsystem/prefab/shell/arrowshell.pfb.17", 4256);
        }

        [Fact]
        public void Rebuild_RE9_CH0200G6000()
        {
            AssertRebuild(GameNames.RE9, "natives/stm/animation/ch/ch02/ch0200/grapple/ch0200g6000/ch0200g6000.pfb.18");
        }

        [Fact]
        public void Rebuild_RE9_IT10_00_006()
        {
            AssertRebuild(GameNames.RE9, "natives/stm/gameassets/item/prefab/layouter/it10_00_006.pfb.18");
        }

        private void AssertRebuild(string gameName, string path, int? expectedLength = null)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new PfbFile(FileVersion.FromPath(path), _pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            // We currently don't keep prefabs that have no owner, so file size might be different
            if (input.Data.Length == output.Data.Length)
            {
                Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            }
            Assert.Equal(expectedLength ?? input.Data.Length, output.Data.Length);
        }
    }
}
