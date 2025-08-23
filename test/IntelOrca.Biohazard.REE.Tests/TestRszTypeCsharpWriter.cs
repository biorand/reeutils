using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestRszTypeCsharpWriter : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Generate()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);
            var userData = repo.FromName("chainsaw.GimmickSaveDataTable")!;

            var csharpWriter = new RszTypeCsharpWriter();
            var output = csharpWriter.Generate(userData);
        }
    }
}
