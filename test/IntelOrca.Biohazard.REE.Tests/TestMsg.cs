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
        public void Temp()
        {
            var path = "natives/stm/_chainsaw/message/mes_main_item/ch_mes_main_item_caption.msg.22";
            var msgData = _pak.GetFileData(path);

            var input = new MsgFile(msgData);
            var inputBuilder = input.ToBuilder();
            var output = inputBuilder.Build();
            var outputBuilder = output.ToBuilder();
        }

        private static PatchedPakFile GetVanillaPak()
        {
            var patch3 = Path.Combine(GetInstallPath(), "re_chunk_000.pak.patch_003.pak");
            return new PatchedPakFile(patch3);
        }

        private static string GetInstallPath() => @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
    }
}
