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
            var test1 = pak.GetEntryData("natives/stm/_biorand/appsystem/test1.dat")!;
            var test2 = pak.GetEntryData("natives/stm/_biorand/authoring/test2.dat")!;
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
            var test1 = pak.GetEntryData("natives/stm/_biorand/appsystem/test1.dat")!;
            var test2 = pak.GetEntryData("natives/stm/_biorand/appsystem/test2.dat")!;
            var test3 = pak.GetEntryData("natives/stm/_biorand/appsystem/test3.dat")!;
            Assert.Equal("test1", Encoding.UTF8.GetString(test1));
            Assert.Equal("test2", Encoding.UTF8.GetString(test2));
            Assert.Equal("test3", Encoding.UTF8.GetString(test3));
        }

        [Fact]
        public void Recreation()
        {
            var dstPath = @"F:\games\steamapps\common\RESIDENT EVIL requiem BIOHAZARD requiem\re_chunk_000.pak.sub_002.pak";
            var pakListPath = @"G:\apps\reasy\resources\data\lists\RE4_STM.list";
            var pakList = PakList.FromFile(pakListPath);

            var pakHelper = new OriginalPakHelper();
            var installPath = pakHelper.GetInstallPath(GameNames.RE4);
            var originaPakPath = Path.Combine(installPath, "re_chunk_000.pak.patch_004.pak");
            var pak = new PakFile(File.ReadAllBytes(originaPakPath));

            var newPak = new PakFileBuilder();
            for (var i = 0; i < pak.EntryCount; i++)
            {
                var fileHash = pak.GetEntryHash(i);
                var fileData = pak.GetEntryData(i);
                newPak.AddEntry($"__HASH__{fileHash}", fileData);
            }
            newPak.Save(dstPath, CompressionKind.Zstd, g_encryptionKey);
        }

        private static readonly byte[] g_encryptionKey = [
            0x34, 0xC7, 0x93, 0x4A, 0xA7, 0x52, 0x94, 0x63, 0x5A, 0x71, 0xA8, 0x39, 0xA0, 0xD6, 0x9B, 0x4B, 0x79, 0xD6, 0x38, 0xD2, 0x06, 0x80, 0x0F, 0xD5, 0x31, 0x5E, 0xA4, 0x57, 0xAF, 0xFB, 0x97, 0x40, 0x6E, 0xA7, 0x44, 0xB7, 0x08, 0xB6, 0x04, 0x75, 0xEC, 0x54, 0xC2, 0xDA, 0x31, 0xB9, 0x19, 0x2B, 0xF7, 0x0C, 0x1C, 0xB2, 0x3B, 0x15, 0xAF, 0x93, 0xFB, 0x60, 0x56, 0x45, 0xC6, 0xFA, 0x71, 0xDE, 0xDA, 0x60, 0xC6, 0x40, 0x28, 0x01, 0x66, 0xD5, 0x3B, 0x0D, 0x7F, 0x0B, 0x6E, 0xD6, 0x44, 0x9D, 0xF1, 0x82, 0xA3, 0xED, 0xC3, 0x50, 0xAD, 0x65, 0xDB, 0x50, 0x8F, 0xE8, 0xA7, 0x57, 0x70, 0x50, 0x8C, 0xC3, 0xDA, 0x0D, 0x9E, 0x0C, 0xC4, 0x8D, 0x6D, 0x73, 0x3C, 0xD1, 0x2E, 0xC6, 0x8F, 0x5E, 0x31, 0x7A, 0x33, 0x71, 0x5A, 0x0F, 0x49, 0x50, 0x66, 0x8E, 0xD1, 0x5E, 0x03, 0x19, 0x2E, 0xBA
        ];
    }
}
