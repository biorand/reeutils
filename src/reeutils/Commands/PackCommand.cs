using System;
using System.ComponentModel;
using System.IO;
using IntelOrca.Biohazard.REE.Package;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class PackCommand : Command<PackCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Input files to pack")]
            [CommandArgument(0, "<input>")]
            public required string[] InputPaths { get; init; }

            [CommandOption("-o|--output")]
            public string? OutputPath { get; init; }

            [CommandOption("-C")]
            public string? BasePath { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.OutputPath == null)
            {
                return ValidationResult.Error($"Output path not specified");
            }
            foreach (var inputPath in settings.InputPaths)
            {
                if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
                {
                    return ValidationResult.Error($"{inputPath} not found");
                }
            }
            return base.Validate(context, settings);
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            var builder = new PakFileBuilder();
            foreach (var inputPath in settings.InputPaths)
            {
                var fullInputPath = Path.GetFullPath(inputPath);
                var basePath = settings.BasePath ?? Environment.CurrentDirectory;
                if (Directory.Exists(fullInputPath))
                {
                    var files = Directory.GetFiles(fullInputPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var relativePath = Path.GetRelativePath(basePath, file).Replace("\\", "/");
                        builder.AddEntry(relativePath, File.ReadAllBytes(file));
                        AnsiConsole.WriteLine(relativePath);
                    }
                }
                else
                {
                    var relativePath = Path.GetFileName(fullInputPath).Replace("\\", "/");
                    builder.AddEntry(relativePath, File.ReadAllBytes(fullInputPath));
                    AnsiConsole.WriteLine(relativePath);
                }
            }

            builder.Save(settings.OutputPath!, CompressionKind.Zstd, g_encryptionKey);
            return 0;
        }

        private static readonly byte[] g_encryptionKey = [
            0x34, 0xC7, 0x93, 0x4A, 0xA7, 0x52, 0x94, 0x63, 0x5A, 0x71, 0xA8, 0x39, 0xA0, 0xD6, 0x9B, 0x4B, 0x79, 0xD6, 0x38, 0xD2, 0x06, 0x80, 0x0F, 0xD5, 0x31, 0x5E, 0xA4, 0x57, 0xAF, 0xFB, 0x97, 0x40, 0x6E, 0xA7, 0x44, 0xB7, 0x08, 0xB6, 0x04, 0x75, 0xEC, 0x54, 0xC2, 0xDA, 0x31, 0xB9, 0x19, 0x2B, 0xF7, 0x0C, 0x1C, 0xB2, 0x3B, 0x15, 0xAF, 0x93, 0xFB, 0x60, 0x56, 0x45, 0xC6, 0xFA, 0x71, 0xDE, 0xDA, 0x60, 0xC6, 0x40, 0x28, 0x01, 0x66, 0xD5, 0x3B, 0x0D, 0x7F, 0x0B, 0x6E, 0xD6, 0x44, 0x9D, 0xF1, 0x82, 0xA3, 0xED, 0xC3, 0x50, 0xAD, 0x65, 0xDB, 0x50, 0x8F, 0xE8, 0xA7, 0x57, 0x70, 0x50, 0x8C, 0xC3, 0xDA, 0x0D, 0x9E, 0x0C, 0xC4, 0x8D, 0x6D, 0x73, 0x3C, 0xD1, 0x2E, 0xC6, 0x8F, 0x5E, 0x31, 0x7A, 0x33, 0x71, 0x5A, 0x0F, 0x49, 0x50, 0x66, 0x8E, 0xD1, 0x5E, 0x03, 0x19, 0x2E, 0xBA
        ];
    }
}
