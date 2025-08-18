using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestPakFile : IDisposable
    {
        private OriginalPakHelper _pakHelper = new();

        public void Dispose()
        {
            _pakHelper.Dispose();
        }

        [Theory]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2", 1398412279)]
        [InlineData("natives/stm/_chainsaw/appsystem/weapon/lasersight/playerlasersightcontrolleruserdata.user.2", -891513479)]
        [InlineData("natives/stm/_chainsaw/appsystem/weaponcustom/weapondetailcustomuserdata.user.2", 98088230)]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2", -946285964)]
        public void Checksum(string path, int expected)
        {
            var fileData = _pakHelper.GetFileData(GameNames.RE4, path)!;
            var actual = MurMur3.HashData(fileData);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task ExtractAll()
        {
            var pakList = new PakList([
                "natives/stm/_authoring/appsystem/authoringchainsaw/extracontents/dlc_1201/dlc_1201.scn.20",
                "natives/stm/_chainsaw/appsystem/catalog/dlc/dlc_1201/charmeffectsettinguserdata_dlc_1201.user.2",
                "natives/stm/_chainsaw/appsystem/catalog/dlc/dlc_1201/extracontentsdialogmessageidsettinguserdata_dlc_1201.user.2",
                "natives/stm/_chainsaw/appsystem/catalog/dlc/dlc_1201/ingameshopitemmodelsettinguserdata_dlc_1201.user.2",
                "natives/stm/_chainsaw/appsystem/catalog/dlc/dlc_1201/weaponmerchantitemmodelparamuserdata_dlc_1201.user.2",
                "natives/stm/_chainsaw/appsystem/prefab/gui/ingameshop/itemmodel/ingameshop_itemmodel_sm79_600_00.pfb.17",
                "natives/stm/_chainsaw/appsystem/ui/dlc/ingameshopitemsettinguserdata_dlc1201.user.2",
                "natives/stm/_chainsaw/environment/texture/sm79_600_a_atos.tex.143221013",
                "natives/stm/_chainsaw/message/dlc/ch_mes_dlc_1201.msg.22",
                "natives/stm/_chainsaw/ui/dlc/dlc_1201/tex/cs_ui0100_1201_iam.tex.143221013",
                "natives/stm/_chainsaw/ui/dlc/dlc_1201/tex/cs_ui0102_1201_iam.tex.143221013",
                "natives/stm/_chainsaw/ui/dlc/dlc_1201/userdata/charmmodelsetting_1201_userdata.user.2",
                "natives/stm/_chainsaw/ui/dlc/dlc_1201/userdata/itemmessageidoverwritesettinguserdata_dlc_1201.user.2",
                "natives/stm/_chainsaw/ui/dlc/dlc_1201/userdata/uvsequenceresourcesetting_1201_userdata.user.2",
                "natives/stm/streaming/_chainsaw/environment/texture/sm79_600_a_albd.tex.143221013"
            ]);

            var dlcPath = Path.Combine(_pakHelper.GetInstallPath(GameNames.RE4), "dlc", "re_dlc_stm_2109314.pak");
            using var pakFile = new PakFile(dlcPath);
            using var tempFolder = new TempFolder();
            await pakFile.ExtractAllAsync(pakList, tempFolder.Path);

            var files = Directory.GetFiles(tempFolder.Path, "*", SearchOption.AllDirectories);
            Assert.Equal(15, files.Length);
            await AssertFile(pakList.Entries[1], -1429291121);
            await AssertFile(pakList.Entries[6], -858496778);

            async Task AssertFile(string path, int expectedHash)
            {
                var discPath = Path.Combine(tempFolder.Path, path);
                var data = await File.ReadAllBytesAsync(discPath);
                Assert.Equal(expectedHash, MurMur3.HashData(data));
            }
        }
    }
}
