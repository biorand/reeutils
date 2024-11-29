using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Namsku.REE.Messages;
using Spectre.Console;
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
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.InputPath))
            {
                return ValidationResult.Error($"{settings.InputPath} not found");
            }
            if (settings.OutputPath == null)
            {
                return ValidationResult.Error($"{settings.OutputPath} not specified");
            }
            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (settings.OutputPath!.EndsWith(".msg.22"))
            {
                var data = File.ReadAllText(settings.InputPath).DeserializeJson<SerializableMsg>();
                var msgBuilder = new Msg.Builder
                {
                    Version = data.Version,
                    Languages = [.. data.Languages],
                    Entries = data.Entries.Select(x => new MsgEntry(data.Version)
                    {
                        Guid = x.Guid,
                        Name = x.Name,
                        Hash = (int)PakHash.GetHash(x.Name),
                        Langs = [.. x.Values]
                    }).ToList()
                };
                var output = msgBuilder.ToMsg();
                await File.WriteAllBytesAsync(settings.OutputPath!, output.Data.ToArray());
            }
            else
            {
                throw new NotSupportedException("File format not supported.");
            }
            return 0;
        }
    }
}
