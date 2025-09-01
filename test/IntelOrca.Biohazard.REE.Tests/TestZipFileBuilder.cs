using System.Text;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestZipFileBuilder
    {
        [Fact]
        public void Build()
        {
            var zipFileBuilder = new ZipFileBuilder();
            zipFileBuilder.AddEntry("test.dat", [0, 1, 2, 3]);
            zipFileBuilder.AddEntry("docs/README.md", Encoding.ASCII.GetBytes("Documentation file..."));
            var zipFile = zipFileBuilder.Build();
            Assert.Equal(247, zipFile.Length);
        }
    }
}
