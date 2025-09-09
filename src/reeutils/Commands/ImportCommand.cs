using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Rsz;
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
                var msgBuilder = new MsgFile.Builder
                {
                    Version = data.Version,
                    Languages = data.Languages.Cast<LanguageId>().ToList(),
                    Messages = data.Entries.Select(x => new Msg
                    {
                        Guid = x.Guid,
                        Crc = MurMur3.HashData(x.Name),
                        Name = x.Name,
                        Values = x.Values.Select((x, i) => new MsgValue((LanguageId)data.Languages[i], x)).ToList()
                    }).ToList()
                }
                ;
                var output = msgBuilder.Build();
                await File.WriteAllBytesAsync(settings.OutputPath!, output.Data.ToArray());
            }
            else if (settings.OutputPath!.EndsWith(".user.2"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var repo = GetRszTypeRepository(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

                JsonDocument data;
                using (var fs = new FileStream(settings.InputPath!, FileMode.Open, FileAccess.Read))
                {
                    data = JsonDocument.Parse(fs);
                }
                var userFile = new UserFile(EmbeddedData.GetFile("empty.user.2")!).ToBuilder(repo);
                userFile.Objects = [(RszObjectNode)RszJsonSerializer.Deserialize(data)];
                await File.WriteAllBytesAsync(settings.OutputPath!, userFile.Build().Data);
            }
            else if (settings.OutputPath!.EndsWith(".scn.20"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var repo = GetRszTypeRepository(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

                JsonDocument data;
                using (var fs = new FileStream(settings.InputPath!, FileMode.Open, FileAccess.Read))
                {
                    data = JsonDocument.Parse(fs);
                }
                var scnFile = new ScnFile(20, EmbeddedData.GetFile("empty.scn.20")!).ToBuilder(repo);
                scnFile.Scene = (RszScene)RszJsonSerializer.Deserialize(data);
                await File.WriteAllBytesAsync(settings.OutputPath!, scnFile.Build().Data);
            }
            else if (settings.OutputPath!.EndsWith(".pfb.17"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var repo = GetRszTypeRepository(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

                JsonDocument data;
                using (var fs = new FileStream(settings.InputPath!, FileMode.Open, FileAccess.Read))
                {
                    data = JsonDocument.Parse(fs);
                }
                var pfbFile = new PfbFile(17, EmbeddedData.GetFile("empty.pfb.17")!).ToBuilder(repo);
                pfbFile.Scene = (RszScene)RszJsonSerializer.Deserialize(data);
                await File.WriteAllBytesAsync(settings.OutputPath!, pfbFile.Build().Data);
            }
            else
            {
                throw new NotSupportedException("File format not supported.");
            }
            return 0;
        }

        private static RszTypeRepository? GetRszTypeRepository(string game)
        {
            var rszJsonGz = EmbeddedData.GetCompressedFile($"rsz{game}.json");
            if (rszJsonGz == null)
                return null;

            return RszRepositorySerializer.Default.FromJsonGz(rszJsonGz);
        }
    }
}
