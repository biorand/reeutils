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
                    .WithDescription("Exports a file using its handler-specific format.")
                    .WithExample("export", "-o", "ch_mes_main_item_caption.msg.22.json", "ch_mes_main_item_caption.msg.22")
                    .WithExample("export", "-o", "fsm.dot", "input.fsm.16")
                    .WithExample("export", "-o", "output.dds", "input.tex.143221013");
                config.AddCommand<ImportCommand>("import")
                    .WithDescription("Imports a file using the destination handler.")
                    .WithExample("import", "-o", "ch_mes_main_item_caption.msg.22", "ch_mes_main_item_caption.msg.22.json")
                    .WithExample("import", "-o", "input.fsm.16", "input.fsm.16.json")
                    .WithExample("import", "-o", "input.tex.143221013", "input.dds");
                config.AddCommand<MsgCommand>("msg")
                    .WithDescription("Lists strings in an MSG file")
                    .WithExample("msg", "input.msg.22");
                config.AddCommand<HierarchyCommand>("hierarchy")
                    .WithDescription("Shows the dependency hierarchy for a given pattern.")
                    .WithExample("hierarchy", "input.msg.22", "-g", "re8", @"C:\games\re8");
                config.AddCommand<ClassCommand>("class")
                    .WithDescription("Generates a C# class for an RSZ type.")
                    .WithExample("class", "-g", "re9", "app.InventorySlotCapacitySetting");
                config.AddCommand<InspectCommand>("inspect")
                    .WithDescription("Displays metadata for supported REE file types.")
                    .WithExample("inspect", "input.tex.143221013")
                    .WithExample("inspect", "--pak", "input.pak", "natives/stm/leveldesign/chapter/chap3_01/chap3_01_level.scn.21");
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
                     .WithDescription("Shows the tree or JSON view of a supported REE file.")
                     .WithExample("tree", "chap3_01_level.scn.21", "LightSwitch/Gm99_108", "-g", "re9")
                     .WithExample("tree", "--pak", "input.pak", "-g", "re9", "--json", "natives/stm/leveldesign/chapter/chap3_01/chap3_01_level.scn.21");
                config.AddCommand<McpCommand>("mcp")
                    .WithDescription("Runs reeutils as an MCP stdio server.");
            });
            return app.Run(args);
        }
    }
}
