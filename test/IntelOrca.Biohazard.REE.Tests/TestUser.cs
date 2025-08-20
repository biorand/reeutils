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
            AssertRebuild("natives/stm/_chainsaw/appsystem/ui/userdata/weaponpartscombinedefinitionuserdata.user.2");
        }

        private void AssertRebuild(string path)
        {
            var repo = GetTypeRepository();
            var input = new UserFile(_pakHelper.GetFileData(GameNames.RE4, path));
            var inputBuilder = input.ToBuilder(repo);
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder(repo);

            Assert.True(input.Data.Span.SequenceEqual(output.Data.Span));
            Assert.Equal(input.Data.Length, output.Data.Length);
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
