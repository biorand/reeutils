using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestUser : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Rebuild_RE4_WEAPONPARTSCOMBINEDEFINITIONUSERDATA()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/appsystem/ui/userdata/weaponpartscombinedefinitionuserdata.user.2");
        }

        [Fact]
        public void Rebuild_RE4_WEAPONDETAILCUSTOMUSERDATA()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/appsystem/weaponcustom/weapondetailcustomuserdata.user.2");
        }

        [Fact]
        public void Rebuild_RE4_WEAPONEQUIPPARAMCATALOGUSERDATA()
        {
            AssertRebuild(GameNames.RE4, "natives/stm/_chainsaw/appsystem/weapon/weaponequipparamcataloguserdata.user.2");
        }

        private void AssertRebuild(string gameName, string path)
        {
            var repo = _pakHelper.GetTypeRepository(gameName);
            var input = new UserFile(_pakHelper.GetFileData(gameName, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            Assert.Equal(input.Data.Length, output.Data.Length);
        }
    }
}
