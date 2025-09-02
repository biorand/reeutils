using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestModBuilder : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Fact]
        public void Build_BoltThrowerMod()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.RE4);

            var modBuilder = new ModBuilder();
            modBuilder.Name = "Automatic Bolt Thrower";
            modBuilder.Description = "Changes the infamous bolt thrower\nto be fully automatic.";
            modBuilder.Version = "1.0";
            modBuilder.Author = "IntelOrca";
            modBuilder.ScreenshotFileName = "pic.jpg";
            modBuilder.ScreenshotFileContent = Resources.boltthrowermod;

            UpdateCatalog("natives/stm/_chainsaw/appsystem/weapon/weaponequipparamcataloguserdata.user.2", 18);
            UpdateCatalog("natives/stm/_anotherorder/appsystem/weapon/weaponequipparamcataloguserdata_ao.user.2", 19);

            var pakFileContents = modBuilder.BuildPakFile();
            var fluffyZipFileContents = modBuilder.BuildFluffyZipFile();

            Assert.Equal(26250, pakFileContents.Length);
            Assert.Equal(173667, fluffyZipFileContents.Length);

            // Change behaviour of bolt thrower
            void UpdateCatalog(string catalogPath, int index)
            {
                var userFileBuilder = new UserFile(_pakHelper.GetFileData(GameNames.RE4, catalogPath)).ToBuilder(repo);
                var root = userFileBuilder.Objects[0];
                root = root
                    .Set($"_DataTable[{index}]._WeaponStructureParam.TypeOfReload", 0)
                    .Set($"_DataTable[{index}]._WeaponStructureParam.TypeOfShoot", 1);
                userFileBuilder.Objects = [root];
                var newUserFile = userFileBuilder.Build();
                modBuilder.AddFile(catalogPath, newUserFile.Data);
            }
        }
    }
}
