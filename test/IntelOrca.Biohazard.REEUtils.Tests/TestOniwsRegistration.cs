using System.Linq;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public sealed class TestOniwsRegistration
    {
        [Fact]
        public void SupportedGames_Include_Oniws()
        {
            Assert.Contains("oniws", McpEmbeddedData.GetSupportedGames());
        }

        [Fact]
        public void Oniws_EmbeddedData_Can_Be_Loaded()
        {
            Assert.NotNull(McpEmbeddedData.GetPakList("oniws"));
            var repo = McpEmbeddedData.GetRszTypeRepository("oniws");
            Assert.NotNull(repo.FromName("app.PlayerManager"));
        }

        [Fact]
        public void Oniws_Empty_Templates_Exist()
        {
            Assert.NotNull(EmbeddedData.GetFile("empty.scn.21"));
            Assert.NotNull(EmbeddedData.GetFile("empty.user.3"));
            Assert.NotNull(EmbeddedData.GetFile("empty.pfb.18"));
        }

                [Fact]
                public void Oniws_Empty_Templates_Parse_With_RszVersion16()
                {
                    var repo = McpEmbeddedData.GetRszTypeRepository("oniws")!;

                    var scnData = EmbeddedData.GetFile("empty.scn.21")!;
                    Assert.Equal(16, new ScnFile(21, scnData).RszVersion);
                    new ScnFile(21, scnData).ToBuilder(repo);

                    var userData = EmbeddedData.GetFile("empty.user.3")!;
                    Assert.Equal(16, new UserFile(userData).RszVersion);
                    new UserFile(userData).ToBuilder(repo);

                    var pfbData = EmbeddedData.GetFile("empty.pfb.18")!;
                    Assert.Equal(16, new PfbFile(18, pfbData).RszVersion);
                    new PfbFile(18, pfbData).ToBuilder(repo);
                }
    }
}