using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using IntelOrca.Biohazard.REEUtils;
using IntelOrca.Biohazard.REEUtils.FileTypes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class InspectCommand : AsyncCommand<InspectCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Input file path")]
            [CommandArgument(0, "<input>")]
            public required string InputPath { get; init; }

            [Description("Pak file path")]
            [CommandOption("--pak")]
            public string? PakPath { get; init; }

            [Description("Game used for optional RSZ enrichment")]
            [CommandOption("-g|--game")]
            public string? Game { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.PakPath != null && !File.Exists(settings.PakPath))
                return ValidationResult.Error($"{settings.PakPath} not found");

            if (settings.PakPath == null && !File.Exists(settings.InputPath))
                return ValidationResult.Error($"{settings.InputPath} not found");

            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var data = settings.PakPath == null
                ? await File.ReadAllBytesAsync(settings.InputPath)
                : ReadFromPak(settings.PakPath, settings.InputPath);

            var repository = GetRszTypeRepository(settings.Game);
            var handler = FileHandlerFactory.Default.Create(settings.InputPath, data, repository);

            foreach (var (key, value) in handler.GetSummary())
            {
                Print(key, FormatValue(value));
            }

            return ExitCodes.Ok;
        }

        private static byte[] ReadFromPak(string pakPath, string entryPath)
        {
            using var pak = new PakFile(pakPath);
            return pak.GetEntryData(entryPath) ?? throw new FileNotFoundException($"{entryPath} not found in pak {pakPath}");
        }

        private static void Print(string label, object? value)
        {
            Console.WriteLine($"{label}: {value}");
        }

        private static string FormatValue(object? value)
        {
            if (value == null)
                return string.Empty;

            if (value is string text)
                return text;

            if (value is IEnumerable enumerable)
            {
                return string.Join(", ", enumerable.Cast<object>());
            }

            return value.ToString() ?? string.Empty;
        }

        private static RszTypeRepository? GetRszTypeRepository(string? game)
        {
            if (game == null)
                return null;

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
