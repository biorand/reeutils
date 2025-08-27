using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace reeexamples
{
    internal class AutomaticBoltThrower
    {
        /// <summary>
        /// Create a pak patch file containing a patched weapon equip param catalog so that the
        /// bolt thrower is automatic.
        /// an edited string.
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
            var pakBuilder = new PakFileBuilder();

            // Update both Leon's and Ada's weapon equip param catalogs
            UpdateCatalog("natives/stm/_chainsaw/appsystem/weapon/weaponequipparamcataloguserdata.user.2", 18);
            UpdateCatalog("natives/stm/_anotherorder/appsystem/weapon/weaponequipparamcataloguserdata_ao.user.2", 19);

            // Save the new pak mod
            pakBuilder.Save("re_chunk_000.pak.patch_005.pak");

            // Change behaviour of bolt thrower
            void UpdateCatalog(string catalogPath, int index)
            {
                // Open the user file
                var userFileBuilder = new UserFile(pak.GetEntryData(catalogPath)).ToBuilder(repo);

                // Most user files just have one object (the root)
                var root = userFileBuilder.Objects[0];

                // Set the parameters for the bolt thrower (index varies for Leon/Ada)
                root = root
                    .Set($"_DataTable[{index}]._WeaponStructureParam.TypeOfReload", 0)
                    .Set($"_DataTable[{index}]._WeaponStructureParam.TypeOfShoot", 1);

                // The library uses immutable objects. Every change creates a new clone of the RSZ object,
                // so we must overwrite root with our new version.

                // Move our new "modified" root back into the user file builder
                userFileBuilder.Objects = [root];

                // Build the user file and add to our pak
                var newUserFile = userFileBuilder.Build();
                pakBuilder.AddEntry(catalogPath, newUserFile.Data);
            }
        }
    }
}
