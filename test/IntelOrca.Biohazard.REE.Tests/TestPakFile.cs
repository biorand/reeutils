using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestPakFile
    {
        [Theory]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2", 1398412279)]
        [InlineData("natives/stm/_chainsaw/appsystem/weapon/lasersight/playerlasersightcontrolleruserdata.user.2", -891513479)]
        [InlineData("natives/stm/_chainsaw/appsystem/weaponcustom/weapondetailcustomuserdata.user.2", 98088230)]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2", -946285964)]
        public void Checksum(string path, int expected)
        {
            using var pakFile = GetVanillaPak();
            var fileData = pakFile.GetFileData(path)!;
            var actual = MurMur3.HashData(fileData);
            Assert.Equal(expected, actual);
        }

        private static PatchedPakFile GetVanillaPak()
        {
            var basePath = @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
            var patch3 = Path.Combine(basePath, "re_chunk_000.pak.patch_003.pak");
            return new PatchedPakFile(patch3);
        }
    }
}