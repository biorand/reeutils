using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Package;

namespace reeexamples
{
    internal class Messages
    {
        /// <summary>
        /// Create a pak patch file containing a new version of a MSG file which has
        /// an edited string.
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            var path = @"F:\games\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
            var msgFilePath = "natives/stm/_chainsaw/message/mes_main_item/ch_mes_main_item_caption.msg.22";

            // Load in all the game's PAK files
            var pak = new RePakCollection(path);

            // Get the MSG file we want to edit
            var oldMsgFile = new MsgFile(pak.GetEntryData(msgFilePath));

            // Build a new MSG file with the changes
            var msgBuilder = oldMsgFile.ToBuilder();
            msgBuilder.SetString(
                new Guid("6db71d5a-9954-48f0-acf1-96bdd59efbde"),
                LanguageId.English,
                "A strange lion head.");
            var newMsgFile = msgBuilder.Build();

            // Create a new pak file with our patched MSG file
            var pakBuilder = new PakFileBuilder();
            pakBuilder.AddEntry(msgFilePath, newMsgFile.Data);
            pakBuilder.Save("re_chunk_000.pak.patch_005.pak");
        }
    }
}
