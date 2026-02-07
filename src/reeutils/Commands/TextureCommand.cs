using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Textures;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class TextureCommand : AsyncCommand<TextureCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Input file (.tex.* or image)")]
            [CommandArgument(0, "<input>")]
            public required string InputPath { get; init; }

            [CommandOption("-o|--output")]
            [Description("Output file path")]
            public required string OutputPath { get; init; }

            [CommandOption("-f|--format")]
            [Description("Target DXGI Format ID (uint)")]
            public uint? Format { get; init; }

            [CommandOption("-g|--game")]
            [Description("Target Game (e.g. re2, re3, re8, re4) - automatically sets version")]
            public string? Game { get; init; }

            [CommandOption("-v|--version")]
            [Description("Target Header Version (default 36, or same as input if converting tex->tex)")]
            public int? Version { get; init; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.InputPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Input file '{settings.InputPath}' not found.");
                return ExitCodes.FileNotFound;
            }

            try
            {
                string ext = Path.GetExtension(settings.InputPath).ToLower();
                bool isTex = settings.InputPath.Contains(".tex") || ext.StartsWith(".tex");

                if (isTex)
                {
                    AnsiConsole.MarkupLine($"Reading RE Engine Texture: [green]{settings.InputPath}[/]");
                    var texFile = new ReTextureFile();
                    using (var fs = File.OpenRead(settings.InputPath))
                    {
                        texFile.Read(fs);
                    }

                    AnsiConsole.MarkupLine($"Loaded. Header Version: [blue]{texFile.Version}[/]");
                    AnsiConsole.MarkupLine($"Dimensions: [blue]{texFile.Header.Width}x{texFile.Header.Height}[/]");

                    // Export
                    using (var fs = File.OpenRead(settings.InputPath))
                    {
                        texFile.ExportToTga(settings.OutputPath, fs);
                    }
                    AnsiConsole.MarkupLine($"Exported to [green]{settings.OutputPath}[/]");
                }
                else
                {
                    // Import
                    AnsiConsole.MarkupLine($"Importing Image: [green]{settings.InputPath}[/]");
                    var texFile = new ReTextureFile();

                    uint? targetFormat = settings.Format;

                    int version = 36; // Default
                    if (settings.Version.HasValue)
                    {
                         version = settings.Version.Value;
                    }
                    else if (!string.IsNullOrEmpty(settings.Game))
                    {
                        version = GetVersionFromGame(settings.Game);
                        AnsiConsole.MarkupLine($"Game '{settings.Game}' mapped to version [blue]{version}[/]");
                    }
                    else
                    {
                        // Try to infer from extension
                        var outExt = Path.GetExtension(settings.OutputPath);
                        if (int.TryParse(outExt.TrimStart('.'), out int v))
                        {
                            version = v;
                        }
                        else
                        {
                            var name = Path.GetFileName(settings.OutputPath);
                            var parts = name.Split('.');
                            if (parts.Length > 2 && parts[parts.Length - 2] == "tex")
                            {
                                if (int.TryParse(parts[parts.Length - 1], out int v2)) version = v2;
                            }
                        }
                    }

                    // This might block the async thread slightly but ImageSharp load is sync in the original code too
                    // wrapping in task run if needed, but for CLI tool it's fine.
                     await Task.Run(() => 
                     {
                         texFile.ImportFromImage(settings.InputPath, targetFormat, version);
                     });
                    
                    AnsiConsole.MarkupLine($"Processed. Target Version: [blue]{version}[/]");
                    AnsiConsole.MarkupLine($"Mips generated: [blue]{texFile.Mips.Count}[/]");

                    using (var fs = File.Create(settings.OutputPath))
                    {
                        texFile.Write(fs);
                    }
                    AnsiConsole.MarkupLine($"Written to [green]{settings.OutputPath}[/]");
                }
                return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                return 1;
            }
        }

        private int GetVersionFromGame(string game)
        {
            return game.ToLowerInvariant() switch
            {
                "re7" => 10,
                "re2" => 10, // Early RE2 builds used 10, final usually 190820018 -> 10 mapping
                "re2r" => 10,
                "re3" => 190820018, // 10
                "re3r" => 190820018,
                "re8" => 36, // Village
                "re4" => 36, // RE4 Remake
                "re4r" => 36,
                "sf6" => 36,
                "mhrize" => 36, // Rise
                "dd2" => 36, // Dragons Dogma 2 (uses newer sometimes, but 36 often works or is base)
                _ => 36 // Default modern
            };
        }
    }
}
