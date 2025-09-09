using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;
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

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.BaselinePaths.Length == 0 && !File.Exists(settings.InputPath))
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
            var jsonOptions = new JsonSerializerOptions()
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            var fileData = GetFileData(settings);
            if (fileData == null)
            {
                Console.Error.WriteLine($"{settings.InputPath} not found");
                return ExitCodes.FileNotFound;
            }

            if (settings.InputPath.EndsWith(".msg.22"))
            {
                var msg = new MsgFile(fileData).ToBuilder();
                var data = new SerializableMsg
                {
                    Version = msg.Version,
                    Languages = [.. msg.Languages.Cast<int>()],
                    Entries = msg.Messages.Select(x => new SerializableMsg.Entry()
                    {
                        Guid = x.Guid,
                        Name = x.Name,
                        Values = [.. x.Values.Select(x => x.Text)]
                    }).ToArray()
                };
                await File.WriteAllTextAsync(settings.OutputPath!, data.ToJson(camelCase: true));
            }
            else if (settings.InputPath.EndsWith(".user.2"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var repo = GetRszTypeRepository(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");
                var userFile = new UserFile(fileData);
                var root = userFile.GetObjects(repo)[0];
                var rootJson = RszJsonSerializer.Serialize(root, jsonOptions);
                await File.WriteAllTextAsync(settings.OutputPath!, rootJson);
            }
            else if (settings.InputPath.EndsWith(".scn.20"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var repo = GetRszTypeRepository(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");
                var scnFile = new ScnFile(20, fileData);
                var root = scnFile.ReadScene(repo);
                var rootJson = RszJsonSerializer.Serialize(root, jsonOptions);
                await File.WriteAllTextAsync(settings.OutputPath!, rootJson);
            }
            else if (settings.InputPath.EndsWith(".pfb.17"))
            {
                if (settings.Game == null)
                    throw new Exception("Game not specified");

                var repo = GetRszTypeRepository(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");
                var pfbFile = new PfbFile(17, fileData);
                var root = pfbFile.ReadScene(repo);
                var rootJson = RszJsonSerializer.Serialize(root, jsonOptions);
                await File.WriteAllTextAsync(settings.OutputPath!, rootJson);
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
