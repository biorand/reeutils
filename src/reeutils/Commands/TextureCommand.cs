using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Graphics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class TextureCommand : AsyncCommand<TextureCommand.Settings>
    {
        private static readonly TextureConvertOptions TextureConvertOptions = new TextureConvertOptions
        {
            Encoder = IntelOrca.Biohazard.REEUtils.GDeflate.Instance
        };

        public sealed class Settings : CommandSettings
        {
            [Description("Input file (.tex.* or .dds)")]
            [CommandArgument(0, "<input>")]
            public required string InputPath { get; init; }

            [CommandOption("-o|--output")]
            [Description("Output file path")]
            public required string OutputPath { get; init; }

            [CommandOption("-g|--game")]
            [Description("Target Game (e.g. re2, re3, re8, re4) - automatically sets version")]
            public string? Game { get; init; }

            [CommandOption("-v|--version")]
            [Description("Target Header Version (default 36, or same as input if converting tex->tex)")]
            public int? Version { get; init; }
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.InputPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Input file '{settings.InputPath}' not found.");
                return Task.FromResult(ExitCodes.FileNotFound);
            }

            try
            {
                var isTex = Path.GetFileNameWithoutExtension(settings.InputPath).Contains(".tex", StringComparison.OrdinalIgnoreCase)
                    || settings.InputPath.Contains(".tex.", StringComparison.OrdinalIgnoreCase);

                if (isTex)
                {
                    AnsiConsole.MarkupLine($"Reading RE Engine Texture: [green]{settings.InputPath}[/]");
                    var texFile = new TextureFile(File.ReadAllBytes(settings.InputPath));

                    AnsiConsole.MarkupLine($"Loaded. Header Version: [blue]{texFile.RawVersion}[/]");
                    AnsiConsole.MarkupLine($"Dimensions: [blue]{texFile.Width}x{texFile.Height}[/]");

                    var dds = texFile.ToDds(TextureConvertOptions);
                    File.WriteAllBytes(settings.OutputPath, dds.ToBytes());
                    AnsiConsole.MarkupLine($"Exported to [green]{settings.OutputPath}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"Importing DDS: [green]{settings.InputPath}[/]");
                    var dds = DdsFile.Read(File.ReadAllBytes(settings.InputPath));

                    var version = settings.Version
                        ?? (settings.Game != null ? GetVersionFromGame(settings.Game) : GetVersionFromExtension(settings.OutputPath));
                    if (settings.Game != null)
                        AnsiConsole.MarkupLine($"Game '{settings.Game}' mapped to version [blue]{version}[/]");

                    var texFile = dds.ToTextureFile(version, TextureConvertOptions);
                    File.WriteAllBytes(settings.OutputPath, texFile.Data.ToArray());
                    AnsiConsole.MarkupLine($"Written to [green]{settings.OutputPath}[/]");
                }
                return Task.FromResult(ExitCodes.Ok);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                return Task.FromResult(1);
            }
        }

        private static int GetVersionFromExtension(string outputPath)
        {
            var name = Path.GetFileName(outputPath);
            var parts = name.Split('.');
            if (parts.Length > 2 && string.Equals(parts[^2], "tex", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[^1], out var version))
            {
                return version;
            }
            return 36;
        }

        private static int GetVersionFromGame(string game) => game.ToLowerInvariant() switch
        {
            "re7" => 10,
            "re2" or "re2r" => 10,
            "re3" or "re3r" => 190820018,
            "re8" or "re4" or "re4r" => 36,
            _ => 36
        };
    }
}
