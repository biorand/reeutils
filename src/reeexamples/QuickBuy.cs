using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace reeexamples
{
    internal class QuickBuy
    {
        /// <summary>
        /// Create a pak patch file containing patched gui param settings so that the
        /// buying items from the shop is instant (click rather than hold down).
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            // We need an RSZ json file which documents all the types for the game
            // We can get one from [REasy](https://github.com/seifhassine/REasy)
            var repo = RszRepositorySerializer.Default.FromJsonFile("rszre4.json");

            // Load in all the game's PAK files
            // WARNING: Make sure game directory has no fluffy mods installed
            var path = @"C:\Program Files (x86)\Steam\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
            var pak = new RePakCollection(path);

            // Open the user file
            const string guiParamPath = "natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2";
            var userFileBuilder = new UserFile(pak.GetEntryData(guiParamPath)).ToBuilder(repo);

            // Most user files just have one object (the root)
            var root = userFileBuilder.Objects[0];

            // The library uses immutable objects. Every change creates a new clone of the RSZ object,
            // so we must overwrite root with our new version.
            root = root.Set("_InGameShopGuiParamHolder._HoldTime_Purchase", 0.1f);

            // Move our new "modified" root back into the user file builder
            userFileBuilder.Objects = [root];

            // Build the user file and add to our pak
            var newUserFile = userFileBuilder.Build();

            // Save the new pak mod
            var pakBuilder = new PakFileBuilder();
            pakBuilder.AddEntry(guiParamPath, newUserFile.Data);
            pakBuilder.Save("re_chunk_000.pak.patch_005.pak");
        }
    }
}
