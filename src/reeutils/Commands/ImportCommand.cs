using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class ImportCommand : AsyncCommand<ImportCommand.Settings>
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
        }

        public override Spectre.Console.ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.InputPath))
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
            JsonDocument document;
            using (var stream = new FileStream(settings.InputPath, FileMode.Open, FileAccess.Read))
            {
                document = JsonDocument.Parse(stream);
            }

            var repository = settings.Game == null ? null : GetRszTypeRepository(settings.Game);
            var handler = FileHandlerFactory.Default.Create(settings.OutputPath!, Array.Empty<byte>(), repository);
            if (handler.RequiresTypeRepository && repository == null)
            {
                throw new InvalidOperationException("Game not specified. Use -g <game>.");
            }

            var bytes = handler.Import(document);
            await File.WriteAllBytesAsync(settings.OutputPath!, bytes);
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
    }
}
