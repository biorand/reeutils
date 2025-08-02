using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REE.Tests
{
    public class TestMsg : IDisposable
    {
        private PatchedPakFile _pak;

        public TestMsg()
        {
            _pak = GetVanillaPak();
        }

        public void Dispose()
        {
            _pak.Dispose();
        }

        [Fact]
        public void Rebuild_RE4_CH_MES_MAIN_ITEM_CAPTION()
        {
            var path = "natives/stm/_chainsaw/message/mes_main_item/ch_mes_main_item_caption.msg.22";
            var input = new MsgFile(_pak.GetFileData(path));
            var inputBuilder = input.ToBuilder();
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder();

            Assert.Equal(inputBuilder.Version, outputBuilder.Version);
            Assert.Equal(inputBuilder.Languages, outputBuilder.Languages);
            Assert.Equal(inputBuilder.Messages.Count, outputBuilder.Messages.Count);
            for (var i = 0; i < inputBuilder.Messages.Count; i++)
            {
                var inputMsg = inputBuilder.Messages[i];
                var outputMsg = outputBuilder.Messages[i];
                Assert.Equal(inputMsg.Guid, outputMsg.Guid);
                Assert.Equal(inputMsg.Crc, outputMsg.Crc);
                Assert.Equal(inputMsg.Name, outputMsg.Name);
                Assert.Equal(inputMsg.Values.Length, outputMsg.Values.Length);
                for (var j = 0; j < inputMsg.Values.Length; j++)
                {
                    Assert.Equal(inputMsg.Values[j], outputMsg.Values[j]);
                }
            }
        }

        [Fact]
        public void GetMessage_RE4_CH_MES_MAIN_ITEM_CAPTION()
        {
            var path = "natives/stm/_chainsaw/message/mes_main_item/ch_mes_main_item_caption.msg.22";
            var input = new MsgFile(_pak.GetFileData(path));
            var msg52 = input.GetMessage(52);
            Assert.Equal(new Guid("6db71d5a-9954-48f0-acf1-96bdd59efbde"), msg52.Guid);
            Assert.Equal(0x5fb85666, msg52.Crc);
            Assert.Equal("CH_Mes_Main_ITEM_CAPTION_74_514_00_0_000", msg52.Name);
            Assert.Equal(LanguageId.English, msg52.Values[1].Language);
            Assert.Equal("A statue piece in the\r\nshape of a lion's head.", msg52.Values[1].Text);
        }

        private static PatchedPakFile GetVanillaPak()
        {
            var patch3 = Path.Combine(GetInstallPath(), "re_chunk_000.pak.patch_003.pak");
            return new PatchedPakFile(patch3);
        }

        private static string GetInstallPath() => @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
    }
}
