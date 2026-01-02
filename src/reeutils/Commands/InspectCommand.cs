using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class InspectCommand : AsyncCommand<InspectCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Input pak file")]
            [CommandArgument(0, "<input>")]
            public required string InputPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [CommandOption("-l|--pak-list")]
            public string? PakListPath { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.InputPath) && !Directory.Exists(settings.InputPath))
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
            var pakList = settings.PakListPath == null
                ? EmbeddedData.GetPakList(settings.Game!) ?? throw new Exception($"{settings.Game} not recognized.")
                : new PakList(File.ReadAllText(settings.PakListPath));
            var rszTypeRepo = GetRszTypeRepository(settings.Game ?? throw new Exception($"{settings.Game} not recognized."))
                ?? throw new Exception("Failed to open RSZ type repository.");

            var finder = new PakPathFinder(rszTypeRepo, pakFile);
            var totalHashes = pakFile.EntryCount;
            var unknownHashes = finder.GetUnknownHashes(pakList);
            var foundPaths = finder.Find(pakList);

            Console.WriteLine($"{totalHashes} files in pak file");
            Console.WriteLine($"{unknownHashes.Length} unknown");
            Console.WriteLine($"{foundPaths.Length} figured out");

            foreach (var p in foundPaths)
            {
                Console.WriteLine(p);
            }
            return 0;
        }

        private static RszTypeRepository? GetRszTypeRepository(string game)
        {
            var rszJsonGz = EmbeddedData.GetFile($"rsz{game}.json.gz");
            if (rszJsonGz == null)
                return null;

            return RszRepositorySerializer.Default.FromJsonGz(rszJsonGz);
        }
    }
}
