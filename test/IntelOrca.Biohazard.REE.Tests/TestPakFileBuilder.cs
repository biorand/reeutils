using System.Text;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestPakFileBuilder
    {
        [Fact]
        public void InMemory_Hash()
        {
            var builder = new PakFileBuilder();
            builder.AddEntry("natives/stm/_biorand/appsystem/test1.dat", Encoding.UTF8.GetBytes("test_test1"));
            builder.AddEntry("natives/stm/_biorand/appsystem/test2.dat", Encoding.UTF8.GetBytes("test_test2"));
            var pakData = builder.ToByteArray();
            var actual = MurMur3.HashData(pakData);
            Assert.Equal(-149172331, actual);
        }

        [Fact]
        public void InMemory_Files()
        {
            var builder = new PakFileBuilder();
            builder.AddEntry("natives/stm/_biorand/appsystem/test1.dat", Encoding.UTF8.GetBytes("test_test1"));
            builder.AddEntry("natives/stm/_biorand/authoring/test2.dat", Encoding.UTF8.GetBytes("test_test2"));
            var pak = builder.ToPakFile();
            Assert.Equal(2, pak.EntryCount);
            var test1 = pak.GetFileData("natives/stm/_biorand/appsystem/test1.dat")!;
            var test2 = pak.GetFileData("natives/stm/_biorand/authoring/test2.dat")!;
            Assert.Equal("test_test1", Encoding.UTF8.GetString(test1));
            Assert.Equal("test_test2", Encoding.UTF8.GetString(test2));
        }

        [Fact]
        public void ToDisc()
        {
            using var tempFolder = new TempFolder();
            var pakPath = Path.Combine(tempFolder.Path, "test.pak");

            var fileFromDiscPath = Path.Combine(tempFolder.Path, "test3.dat");
            File.WriteAllText(fileFromDiscPath, "test3");

            var builder = new PakFileBuilder();
            builder.AddEntry("natives/stm/_biorand/appsystem/test1.dat", Encoding.UTF8.GetBytes("test1"));
            builder.AddEntry("natives/stm/_biorand/appsystem/test2.dat", Encoding.UTF8.GetBytes("test2"));
            builder.AddEntry("natives/stm/_biorand/appsystem/test3.dat", fileFromDiscPath);
            builder.Save(pakPath, CompressionKind.Zstd);

            var pak = new PakFile(pakPath);
            Assert.Equal(3, pak.EntryCount);
            var test1 = pak.GetFileData("natives/stm/_biorand/appsystem/test1.dat")!;
            var test2 = pak.GetFileData("natives/stm/_biorand/appsystem/test2.dat")!;
            var test3 = pak.GetFileData("natives/stm/_biorand/appsystem/test3.dat")!;
            Assert.Equal("test1", Encoding.UTF8.GetString(test1));
            Assert.Equal("test2", Encoding.UTF8.GetString(test2));
            Assert.Equal("test3", Encoding.UTF8.GetString(test3));
        }
    }
}
