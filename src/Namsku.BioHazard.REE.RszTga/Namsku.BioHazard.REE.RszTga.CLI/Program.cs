using CommandLine;
using Namsku.BioHazard.REE.RszTga.Core;
using DirectXTexNet;

namespace Namsku.BioHazard.REE.RszTga.CLI;

public class Options
{
    [Option('i', "input", Required = true, HelpText = "Input file path (.tex.* or image)")]
    public string Input { get; set; } = string.Empty;

    [Option('o', "output", Required = true, HelpText = "Output file path")]
    public string Output { get; set; } = string.Empty;

    [Option('f', "format", HelpText = "Target DXGI Format ID (uint)")]
    public uint? Format { get; set; }
    
    [Option('v', "version", HelpText = "Target Header Version (default 36, or same as input if converting tex->tex)")]
    public int? Version { get; set; }
}

public class Program
{
    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(RunOptions)
            .WithNotParsed(HandleParseError);
    }

    private static void RunOptions(Options opts)
    {
        try
        {
            if (!File.Exists(opts.Input))
            {
                Console.WriteLine($"Error: Input file '{opts.Input}' not found.");
                return;
            }

            string ext = Path.GetExtension(opts.Input).ToLower();
            bool isTex = opts.Input.Contains(".tex") || ext.StartsWith(".tex"); // simple check

            if (isTex)
            {
                Console.WriteLine($"Reading RE Engine Texture: {opts.Input}");
                var texFile = new ReTextureFile();
                using (var fs = File.OpenRead(opts.Input))
                {
                    texFile.Read(fs);
                }
                
                Console.WriteLine($"Loaded. Header Version: {texFile.Version}");
                Console.WriteLine($"Dimensions: {texFile.Header.Width}x{texFile.Header.Height}");
                
                // Export
                using (var fs = File.OpenRead(opts.Input))
                {
                    texFile.ExportToTga(opts.Output, fs);
                }
                Console.WriteLine($"Exported to {opts.Output}");
            }
            else
            {
                // Import
                Console.WriteLine($"Importing Image: {opts.Input}");
                var texFile = new ReTextureFile();
                
                DXGI_FORMAT fmt = DXGI_FORMAT.UNKNOWN;
                if (opts.Format.HasValue) fmt = (DXGI_FORMAT)opts.Format.Value;
                
                int version = opts.Version ?? 36;
                // Try to infer version from output extension if not specified? 
                // e.g. .tex.10 -> version 10
                if (!opts.Version.HasValue)
                {
                    var outExt = Path.GetExtension(opts.Output); // .10?
                    if (int.TryParse(outExt.TrimStart('.'), out int v))
                    {
                        version = v;
                    }
                    else
                    {
                        // Check double extension?
                        var name = Path.GetFileName(opts.Output);
                        var parts = name.Split('.');
                        if (parts.Length > 2 && parts[parts.Length-2] == "tex")
                        {
                            if (int.TryParse(parts[parts.Length-1], out int v2)) version = v2;
                        }
                    }
                }
                
                texFile.ImportFromImage(opts.Input, fmt, version);
                Console.WriteLine($"Processed. Target Version: {version}");
                Console.WriteLine($"Mips generated: {texFile.Mips.Count}");
                
                using (var fs = File.Create(opts.Output))
                {
                    texFile.Write(fs);
                }
                Console.WriteLine($"Written to {opts.Output}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void HandleParseError(IEnumerable<Error> errs)
    {
        // Help text is auto-generated
    }
}
