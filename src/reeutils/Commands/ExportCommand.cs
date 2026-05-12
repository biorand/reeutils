using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class ExportCommand : AsyncCommand<ExportCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Input file")]
            [CommandArgument(0, "<input>")]
            public required string InputPath { get; init; }

            [CommandOption("-o|--output")]
            public string? OutputPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [CommandOption("-I")]
            public string[] BaselinePaths { get; init; } = [];
        }

        public override Spectre.Console.ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.BaselinePaths.Length == 0 && !File.Exists(settings.InputPath))
            {
                return Spectre.Console.ValidationResult.Error($"{settings.InputPath} not found");
            }
            if (settings.OutputPath == null)
            {
                return Spectre.Console.ValidationResult.Error($"{settings.OutputPath} not specified");
            }
            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var fileData = GetFileData(settings);
            if (fileData == null)
            {
                Console.Error.WriteLine($"{settings.InputPath} not found");
                return ExitCodes.FileNotFound;
            }

            var repository = settings.Game == null ? null : GetRszTypeRepository(settings.Game);
            var handler = FileHandlerFactory.Default.Create(settings.InputPath, fileData, repository);
            if (handler.RequiresTypeRepository && repository == null)
            {
                throw new InvalidOperationException("Game not specified. Use -g <game>.");
            }

            using var json = handler.GetJson(TreeOptions.Root);
            await File.WriteAllTextAsync(settings.OutputPath!, JsonSupport.ToJsonString(json));
            return 0;
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

        private static byte[]? GetFileData(Settings settings)
        {
            if (settings.BaselinePaths.Length != 0)
            {
                foreach (var p in settings.BaselinePaths.Reverse())
                {
                    if (p.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
                    {
                        var pak = new PakFile(p);
                        var data = pak.GetEntryData(settings.InputPath);
                        if (data != null)
                        {
                            return data;
                        }
                    }
                    else
                    {
                        var fullPath = Path.Combine(p, settings.InputPath);
                        if (File.Exists(fullPath))
                        {
                            return File.ReadAllBytes(fullPath);
                        }
                    }
                }
            }
            else
            {
                return File.ReadAllBytes(settings.InputPath);
            }
            return null;
        }

    }
}
