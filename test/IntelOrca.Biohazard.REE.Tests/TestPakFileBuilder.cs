using System.Text;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestPakFileBuilder
    {
        [Fact]
        public void InMemory()
        {
            var builder = new PakFileBuilder();
            builder.AddEntry("natives/stm/_biorand/appsystem/test1.dat", Encoding.UTF8.GetBytes("test_test1"));
            builder.AddEntry("natives/stm/_biorand/appsystem/test2.dat", Encoding.UTF8.GetBytes("test_test2"));
            var pakData = builder.ToByteArray();
            var actual = MurMur3.HashData(pakData);
            Assert.Equal(-149172331, actual);
        }
    }
}