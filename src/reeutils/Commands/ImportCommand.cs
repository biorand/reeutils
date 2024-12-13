using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Namsku.REE.Messages;
using RszTool;
using Spectre.Console;
using Spectre.Console.Cli;
using PakHash = Namsku.REE.Messages.PakHash;

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
            else if (settings.OutputPath!.EndsWith(".user.2"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var rszFileOption = EmbeddedData.CreateRszFileOptionBinary(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

                JsonDocument data;
                using (var fs = new FileStream(settings.InputPath!, FileMode.Open, FileAccess.Read))
                {
                    data = JsonDocument.Parse(fs);
                }
                using var ms = new MemoryStream(EmbeddedData.GetFile("empty.user.2")!);
                var userFile = new UserFile(rszFileOption, new FileHandler(ms));
                userFile.Read();
                userFile.RSZ!.ObjectList.Clear();

                var serializer = new RszInstanceSerializer(userFile.RSZ);
                userFile.RSZ!.ObjectList.Add(serializer.DeserializeUserFile(data.RootElement));

                userFile.RSZ!.RebuildInstanceInfo();
                userFile.RebuildInfoTable();
                await File.WriteAllBytesAsync(settings.OutputPath!, userFile.ToByteArray());
            }
            else if (settings.OutputPath!.EndsWith(".scn.20"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var rszFileOption = EmbeddedData.CreateRszFileOptionBinary(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

                JsonDocument data;
                using (var fs = new FileStream(settings.InputPath!, FileMode.Open, FileAccess.Read))
                {
                    data = JsonDocument.Parse(fs);
                }
                using var ms = new MemoryStream(EmbeddedData.GetFile("empty.scn.20")!);
                var scnFile = new ScnFile(rszFileOption, new FileHandler(ms));
                scnFile.Read();
                scnFile.SetupGameObjects();

                var serializer = new RszInstanceSerializer(scnFile.RSZ!);
                serializer.DeserializeScnFile(scnFile, data.RootElement);

                scnFile.RebuildInfoTable();
                await File.WriteAllBytesAsync(settings.OutputPath!, scnFile.ToByteArray());
            }
            else if (settings.OutputPath!.EndsWith(".pfb.17"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var rszFileOption = EmbeddedData.CreateRszFileOptionBinary(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

                JsonDocument data;
                using (var fs = new FileStream(settings.InputPath!, FileMode.Open, FileAccess.Read))
                {
                    data = JsonDocument.Parse(fs);
                }
                using var ms = new MemoryStream(EmbeddedData.GetFile("empty.pfb.17")!);
                var pfbFile = new PfbFile(rszFileOption, new FileHandler(ms));
                pfbFile.Read();
                pfbFile.SetupGameObjects();

                var serializer = new RszInstanceSerializer(pfbFile.RSZ!);
                serializer.DeserializePfbFile(pfbFile, data.RootElement);

                pfbFile.RebuildInfoTable();
                await File.WriteAllBytesAsync(settings.OutputPath!, pfbFile.ToByteArray());
            }
            else
            {
                throw new NotSupportedException("File format not supported.");
            }
            return 0;
        }
    }
}
