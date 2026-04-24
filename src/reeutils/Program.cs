using IntelOrca.Biohazard.REEUtils.Commands;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                // config.PropagateExceptions();
                config.Settings.ApplicationName = "reeutils";
                config.Settings.StrictParsing = true;
                config.AddCommand<PackCommand>("pack")
                    .WithDescription("Creates a .pak file from the given input files")
                    .WithExample("pack", "-o", "output.pak", "./mod");
                config.AddCommand<UnpackCommand>("unpack")
                    .WithDescription("Extracts a .pak file to a directory")
                    .WithExample("unpack", "-g", "re4r", "-o", "output", "input.pak")
                    .WithExample("unpack", "-l", "mylist.txt", "-o", "output", "input.pak");
                config.AddCommand<ExportCommand>("export")
                    .WithDescription("Export an REE file to JSON.")
                    .WithExample("export", "-o", "ch_mes_main_item_caption.msg.22.json", "ch_mes_main_item_caption.msg.22");
                config.AddCommand<ImportCommand>("import")
                    .WithDescription("Import a JSON file and convert to an REE file.")
                    .WithExample("import", "-o", "ch_mes_main_item_caption.msg.22", "ch_mes_main_item_caption.msg.22.json");
                config.AddCommand<MsgCommand>("msg")
                    .WithDescription("Lists strings in an MSG file")
                    .WithExample("msg", "input.msg.22");
                config.AddCommand<HierarchyCommand>("hierarchy")
                    .WithDescription("Shows the dependency hierarchy for a given pattern.")
                    .WithExample("hierarchy", "input.msg.22", "-g", "re8", @"C:\games\re8");
                config.AddCommand<InspectCommand>("inspect")
                    .WithDescription("Looks through every file in a pak to find paths for a pak list.")
                    .WithExample("inspect", "-g", "re4", "input.pak");
                config.AddCommand<GrepCommand>("grep")
                    .WithDescription("Search files in a pak for properties/values matching a regex.")
                    .WithExample("grep", "--pak", "input.pak", "--regex", "pattern", "natives/stm/**/enemy.user.2");
                config.AddCommand<LsCommand>("ls")
                    .WithDescription("Lists files and directories in a PAK file.")
                    .WithExample("ls", "--pak", "test.pak", "natives/stm");
                config.AddCommand<FindCommand>("find")
                    .WithDescription("Finds files in a PAK file matching the given patterns.")
                    .WithExample("find", "--pak", "test.pak", "-g", "re9", "natives/stm/leveldesign");
                config.AddCommand<TreeCommand>("tree")
                    .WithDescription("Shows the object or scene tree of an REE file.")
                    .WithExample("tree", "--path", "chap3_01_level.scn.21", "-g", "re9")
                    .WithExample("tree", "--pak", "input.pak", "-g", "re9", "natives/stm/leveldesign/chapter/chap3_01/chap3_01_level.scn.21", "Main/LevelPlayerCreateController");
            });
            return app.Run(args);
        }
    }
}
