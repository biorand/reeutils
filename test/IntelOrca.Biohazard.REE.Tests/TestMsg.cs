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
            AssertRebuild("natives/stm/_chainsaw/message/mes_main_item/ch_mes_main_item_caption.msg.22");
        }

        [Fact]
        public void Rebuild_RE4_CH_MES_QUESTFILE_001()
        {
            AssertRebuild("natives/stm/_chainsaw/message/mes_main_questfile/ch_mes_main_questfile_001.msg.22");
        }

        [Fact]
        public void GetMessage_RE4_CH_MES_MAIN_CONV_CP42()
        {
            var path = "natives/stm/_chainsaw/message/mes_main_conv/ch_mes_main_conv_cp42.msg.22";
            var input = new MsgFile(_pak.GetFileData(path));
            var msg2 = input.GetMessage(2);
            Assert.Equal(new Guid("7a607509-3453-4135-b6e8-1dd8b5b6211e"), msg2.Guid);
            Assert.Equal("CH_Mes_Main_Conv_cp42_0001_0020_cha300", msg2.Name);
            Assert.Equal(MsgAttributeType.Wstring, msg2.Attributes[36].Definition.Type);
            Assert.Equal("", msg2.Attributes[36].Definition.Name);
            Assert.Equal("grinning", msg2.Attributes[36].Value);
            Assert.Equal(LanguageId.English, msg2.Values[1].Language);
            Assert.Equal("No need to be suspicious!\r\nI said I'd help you, didn't I?", msg2.Values[1].Text);
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

        private void AssertRebuild(string path)
        {
            var input = new MsgFile(_pak.GetFileData(path));
            var inputBuilder = input.ToBuilder();
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder();

            Assert.Equal(inputBuilder.Version, outputBuilder.Version);
            Assert.Equal(inputBuilder.Languages, outputBuilder.Languages);
            Assert.Equal(inputBuilder.Attributes, outputBuilder.Attributes);
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
                Assert.Equal(inputMsg.Attributes.Length, outputMsg.Attributes.Length);
                for (var j = 0; j < inputMsg.Attributes.Length; j++)
                {
                    Assert.Equal(inputMsg.Attributes[j], outputMsg.Attributes[j]);
                }
            }

            // Check our new file is same size as old one (should be for most cases)
            Assert.Equal(input.Data.Length, output.Data.Length);
        }

        private static PatchedPakFile GetVanillaPak()
        {
            var patch3 = Path.Combine(GetInstallPath(), "re_chunk_000.pak.patch_003.pak");
            return new PatchedPakFile(patch3);
        }

        private static string GetInstallPath() => @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
    }
}
