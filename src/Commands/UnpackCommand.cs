using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using REE;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class UnpackCommand : AsyncCommand<UnpackCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Input pak file")]
            [CommandArgument(0, "<input>")]
            public required string InputPath { get; init; }

            [CommandOption("-o|--output")]
            public string? OutputPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [CommandOption("-l|--pak-list")]
            public string? PakListPath { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.InputPath))
            {
                return ValidationResult.Error($"{settings.InputPath} not found");
            }
            if (settings.PakListPath != null)
            {
                if (!File.Exists(settings.PakListPath))
                {
                    return ValidationResult.Error($"{settings.PakListPath} not found");
                }
            }
            else if (settings.Game == null)
            {
                return ValidationResult.Error($"A game or pak list must be specified.");
            }
            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var pakFile = new PakFile(settings.InputPath);
            var outputPath = settings.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Environment.CurrentDirectory;
            }

            var pakList = settings.PakListPath == null
                ? EmbeddedData.GetPakList(settings.Game!) ?? throw new Exception($"{settings.Game} not recognized.")
                : new PakList(File.ReadAllText(settings.PakListPath));

            await pakFile.ExtractAllAsync(pakList, outputPath);
            return 0;
        }
    }
}
