using System.Text;
using IntelOrca.Biohazard.REE.Cryptography;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestMurMur3
    {
        [Theory]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2", 126103826)]
        [InlineData("natives/stm/_chainsaw/appsystem/weapon/lasersight/playerlasersightcontrolleruserdata.user.2", 807785205)]
        [InlineData("natives/stm/_chainsaw/appsystem/weaponcustom/weapondetailcustomuserdata.user.2", -1806824783)]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2", 2040960699)]
        public void HashData(string str, int expected)
        {
            var actual = MurMur3.HashData(str);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2", 126103826)]
        [InlineData("natives/stm/_chainsaw/appsystem/weapon/lasersight/playerlasersightcontrolleruserdata.user.2", 807785205)]
        [InlineData("natives/stm/_chainsaw/appsystem/weaponcustom/weapondetailcustomuserdata.user.2", -1806824783)]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2", 2040960699)]
        public void ComputeHash(string str, int expected)
        {
            using var hashAlgorithm = MurMur3.Create();
            var actualBytes = hashAlgorithm.ComputeHash(Encoding.Unicode.GetBytes(str));
            var actual = BitConverter.ToInt32(actualBytes, 0);
            Assert.Equal(expected, actual);
        }
    }
}
