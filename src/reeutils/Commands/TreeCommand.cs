using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class TreeCommand : AsyncCommand<TreeCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Pak file path")]
            [CommandOption("--pak")]
            public string? PakPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [Description("Render JSON instead of a Spectre tree")]
            [CommandOption("--json")]
            public bool Json { get; init; }

            [Description("File path (on disk or in pak)")]
            [CommandArgument(0, "<path>")]
            public string? PathArgument { get; init; }

            [Description("Node paths to fully expand")]
            [CommandArgument(1, "[expand_nodes...]")]
            public string[] ExpandNodes { get; init; } = [];

            [Description("Always expand all components")]
            [CommandOption("--full")]
            public bool Full { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            var path = GetFilePath(settings);
            if (string.IsNullOrEmpty(path))
            {
                return ValidationResult.Error("File path not specified. Provide it as the first argument.");
            }
            if (!string.IsNullOrEmpty(settings.PakPath) && !File.Exists(settings.PakPath))
            {
                return ValidationResult.Error($"{settings.PakPath} not found");
            }
            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var path = GetFilePath(settings);

            byte[]? fileData;
            if (!string.IsNullOrEmpty(settings.PakPath))
            {
                var pak = new PakFile(settings.PakPath);
                fileData = pak.GetEntryData(path!);
                if (fileData == null)
                {
                    AnsiConsole.MarkupLine($"[red]File {path} not found in pak.[/]");
                    return ExitCodes.FileNotFound;
                }
            }
            else if (File.Exists(path))
            {
                fileData = await File.ReadAllBytesAsync(path);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]File {path} not found.[/]");
                return ExitCodes.FileNotFound;
            }

            var repository = settings.Game == null ? null : GetRszTypeRepository(settings.Game);
            var handler = FileHandlerFactory.Default.Create(path!, fileData, repository);
            if (handler.RequiresTypeRepository && settings.Game == null)
            {
                AnsiConsole.MarkupLine("[red]Game not specified. Use -g <game>.[/]");
                return ExitCodes.Help;
            }
            if (handler.RequiresTypeRepository && repository == null)
            {
                AnsiConsole.MarkupLine($"[red]{settings.Game} not recognized.[/]");
                return ExitCodes.Help;
            }

            var treeOptions = new TreeOptions
            {
                ExpandNodes = settings.ExpandNodes,
                Full = settings.Full
            };
            if (settings.Json)
            {
                using var json = handler.GetJson(treeOptions);
                Console.WriteLine(JsonSupport.ToJsonString(json));
            }
            else
            {
                AnsiConsole.Write(handler.GetTree(treeOptions));
            }

            return 0;
        }

        private static string? GetFilePath(Settings settings)
        {
            if (!string.IsNullOrEmpty(settings.PathArgument))
                return settings.PathArgument;
            return null;
        }

        private static IntelOrca.Biohazard.REE.Rsz.RszTypeRepository? GetRszTypeRepository(string game)
        {
            try
            {
                return McpEmbeddedData.GetRszTypeRepository(game);
            }
            catch
            {
                return null;
            }
        }
    }
}
