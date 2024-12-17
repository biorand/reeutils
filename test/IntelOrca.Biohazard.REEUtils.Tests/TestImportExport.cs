using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REEUtils.Commands;
using Namsku.REE.Messages;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public class TestImportExport : IDisposable
    {
        private PatchedPakFile _pak;

        public TestImportExport()
        {
            var h = PakHash.GetHash("natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2");
            _pak = GetVanillaPak();
        }

        public void Dispose()
        {
            _pak.Dispose();
        }

        [Theory]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2")]
        [InlineData("natives/stm/_chainsaw/appsystem/weapon/lasersight/playerlasersightcontrolleruserdata.user.2")]
        [InlineData("natives/stm/_chainsaw/appsystem/weaponcustom/weapondetailcustomuserdata.user.2")]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2")]
        public async Task UserFile(string path)
        {
            await CheckFileAsync(path, ".user.2");
        }

        [Theory]
        [InlineData("natives/stm/_anotherorder/leveldesign/chapter/cp11_chp1_1/level_cp11_chp1_1.scn.20")]
        [InlineData("natives/stm/_chainsaw/appsystem/navigation/loc40/navigation_loc4000.scn.20")]
        public async Task ScnFile(string path)
        {
            await CheckFileAsync(path, ".scn.20");
        }

        [Theory]
        [InlineData("natives/stm/_chainsaw/appsystem/prefab/weapon/wp4000.pfb.17")]
        public async Task PfbFile(string path)
        {
            await CheckFileAsync(path, ".pfb.17");
        }

        private async Task CheckFileAsync(string path, string extension)
        {
            using var tempFolder = new TempFolder();
            var userData = _pak.GetFileData(path) ?? throw new Exception();
            var scnPath = tempFolder.GetSubPath($"test{extension}");
            var jsonPath = tempFolder.GetSubPath("test.json");
            File.WriteAllBytes(scnPath, userData);

            var exportCommand = new ExportCommand();
            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = scnPath,
                Game = "re4",
                OutputPath = jsonPath
            });

            var jsonA = File.ReadAllText(jsonPath);

            var importCommand = new ImportCommand();
            await importCommand.ExecuteAsync(null!, new ImportCommand.Settings()
            {
                InputPath = jsonPath,
                Game = "re4",
                OutputPath = scnPath
            });
            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = scnPath,
                Game = "re4",
                OutputPath = jsonPath
            });

            var jsonB = File.ReadAllText(jsonPath);

            Assert.Equal(jsonA, jsonB);
        }

        private PatchedPakFile GetVanillaPak()
        {
            var availablePaths = new string[]
            {
                @"G:\re4r\vanilla",
                @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4"
            };
            var basePath = availablePaths.FirstOrDefault(Directory.Exists);
            Assert.NotNull(basePath);

            var patch3 = Path.Combine(basePath, "re_chunk_000.pak.patch_003.pak");
            return new PatchedPakFile(patch3);
        }
    }
}