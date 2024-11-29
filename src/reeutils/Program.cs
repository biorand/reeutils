using IntelOrca.Biohazard.REEUtils.Commands;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            // return DebugCode();
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
                    .WithExample("export", "-o", "ch_mes_main_item_caption.msg.22", "ch_mes_main_item_caption.msg.22.json");
                // config.AddCommand<DumpCommand>("dump")
                //     .WithDescription("Dumps an SCN file to JSON")
                //     .WithExample("dump", "input.scn");
                config.AddCommand<MsgCommand>("msg")
                    .WithDescription("Lists strings in an MSG file")
                    .WithExample("msg", "input.msg.22");
            });
            return app.Run(args);
        }
    }
}
