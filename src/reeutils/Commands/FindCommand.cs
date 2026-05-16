using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REEUtils;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class FindCommand : AsyncCommand<FindCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--pak")]
            public string? PakPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [CommandOption("-i|--install")]
            public string? InstallPath { get; init; }

            [CommandOption("-l|--pak-list")]
            public string? PakListPath { get; init; }

            [CommandArgument(0, "<patterns...>")]
            public string[] Patterns { get; init; } = Array.Empty<string>();
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (string.IsNullOrEmpty(settings.PakPath) && string.IsNullOrEmpty(settings.Game))
            {
                return ValidationResult.Error("Either --pak <file> or -g <game> (with --install) must be specified.");
            }
            if (!string.IsNullOrEmpty(settings.PakPath) && !File.Exists(settings.PakPath))
            {
                return ValidationResult.Error($"{settings.PakPath} not found");
            }
            if (!string.IsNullOrEmpty(settings.InstallPath) && !Directory.Exists(settings.InstallPath))
            {
                return ValidationResult.Error($"{settings.InstallPath} not found");
            }
            if (settings.Patterns == null || settings.Patterns.Length == 0)
            {
                return ValidationResult.Error("At least one pattern must be specified");
            }
            return base.Validate(context, settings);
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            IPakFile pak;
            try
            {
                if (!string.IsNullOrEmpty(settings.PakPath))
                {
                    pak = new PakFile(settings.PakPath);
                }
                else
                {
                    if (string.IsNullOrEmpty(settings.InstallPath))
                    {
                        AnsiConsole.MarkupLine("[red]When using -g/--game you must also provide --install <installPath>[/]");
                        return Task.FromResult(1);
                    }
                    pak = new RePakCollection(settings.InstallPath);
                }
            }
            catch (Exception e)
            {
                AnsiConsole.MarkupLine($"[red]Failed to open pak: {e.Message}[/]");
                return Task.FromResult(1);
            }

            PakList? pakList = null;
            if (!string.IsNullOrEmpty(settings.PakListPath))
            {
                pakList = new PakList(File.ReadAllText(settings.PakListPath));
            }
            else if (!string.IsNullOrEmpty(settings.Game))
            {
                pakList = EmbeddedData.GetPakList(settings.Game);
                if (pakList == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]Pak list for game '{settings.Game}' not found in embedded data. Names will not be resolved.[/]");
                }
            }

            var existingHashes = new HashSet<ulong>(pak.FileHashes);
            var results = new List<string>();

            if (pakList != null)
            {
                results.AddRange(PakPathMatcher.FindMatchingEntries(pak, pakList, settings.Patterns));
            }
            else
            {
                foreach (var hash in existingHashes)
                {
                    var name = hash.ToString("X16");
                    if (PakPathMatcher.MatchesPatterns(name, settings.Patterns))
                    {
                        results.Add(name);
                    }
                }
            }

            foreach (var r in results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine(r);
            }

            return Task.FromResult(0);
        }
    }
}
