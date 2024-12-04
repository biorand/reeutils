using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
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

                var rszFileOption = EmbeddedData.CreateRszFileOption(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

                JsonDocument data;
                using (var fs = new FileStream(settings.InputPath!, FileMode.Open, FileAccess.Read))
                {
                    data = JsonDocument.Parse(fs);
                }
                using var ms = new MemoryStream(EmbeddedData.GetFile("empty.user.2")!);
                var userFile = new UserFile(rszFileOption, new FileHandler(ms));
                userFile.Read();
                userFile.RSZ!.ObjectList.Clear();
                userFile.RSZ!.ObjectList.Add(DeserializeRootElement(userFile.RSZ, data.RootElement));
                userFile.RSZ!.RebuildInstanceInfo();
                userFile.RebuildInfoTable();
                await File.WriteAllBytesAsync(settings.OutputPath!, userFile.ToByteArray());
            }
            else
            {
                throw new NotSupportedException("File format not supported.");
            }
            return 0;
        }

        private static RszInstance DeserializeRootElement(RSZFile rsz, JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new Exception("Root must be an object");

            return DeserializeObject(rsz, el);
        }

        private static RszInstance DeserializeObject(RSZFile rsz, JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new Exception("Expected object");

            var type = el.GetStringProperty("$type")!;
            var result = rsz.CreateInstance(type);
            foreach (var f in result.Fields)
            {
                if (el.TryGetProperty(f.name, out var propEl))
                {
                    result.SetFieldValue(f.name, DeserializeField(rsz, f, propEl));
                }
            }
            return result;
        }

        private static object DeserializeField(RSZFile rsz, RszField field, JsonElement el)
        {
            return field.array
                ? DeserializeArray(rsz, field.type, el)
                : DeserializeElement(rsz, field.type, el);
        }

        private static object DeserializeArray(RSZFile rsz, RszFieldType type, JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array)
                throw new Exception("Expected array");

            var list = new List<object>();
            foreach (var jArrayItem in el.EnumerateArray())
            {
                list.Add(DeserializeElement(rsz, type, jArrayItem));
            }
            return list;
        }


        private static object DeserializeElement(RSZFile rsz, RszFieldType fieldType, JsonElement el)
        {
            return fieldType switch
            {
                RszFieldType.Bool => el.GetBoolean(),
                RszFieldType.S32 => el.GetInt32(),
                RszFieldType.F32 => el.GetSingle(),
                RszFieldType.Object => DeserializeObject(rsz, el),
                RszFieldType.Vec4 => new Vector4(
                    el.GetProperty("X").GetSingle(),
                    el.GetProperty("Y").GetSingle(),
                    el.GetProperty("Z").GetSingle(),
                    el.GetProperty("W").GetSingle()),
                RszFieldType.Data => BitConverter.GetBytes(el.GetSingle()),
                _ => throw new NotImplementedException(),
            };
        }
    }
}
