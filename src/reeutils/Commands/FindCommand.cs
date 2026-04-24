using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;
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
                foreach (var entry in pakList.Entries)
                {
                    if (string.IsNullOrEmpty(entry))
                        continue;

                    ulong hash;
                    try
                    {
                        hash = ComputeNormalizedPathHash(entry);
                    }
                    catch
                    {
                        continue;
                    }

                    if (!existingHashes.Contains(hash))
                        continue;

                    if (MatchesPatterns(entry, settings.Patterns))
                    {
                        results.Add(entry);
                    }
                }
            }
            else
            {
                foreach (var hash in existingHashes)
                {
                    var name = hash.ToString("X16");
                    if (MatchesPatterns(name, settings.Patterns))
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

        private static bool MatchesPatterns(string entry, string[] patterns)
        {
            foreach (var p in patterns)
            {
                if (p.Contains('*') || p.Contains('?'))
                {
                    var rx = new Regex("^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
                    if (rx.IsMatch(entry))
                        return true;
                }
                else if (entry.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static ulong ComputeNormalizedPathHash(string path)
        {
            path = path.Replace("\\", "/");
            if (path.Contains("__Unknown"))
            {
                var pathWithoutExtension = Path.GetFileNameWithoutExtension(path);
                return Convert.ToUInt64(pathWithoutExtension, 16);
            }
            else
            {
                var lower = (uint)MurMur3.HashData(path.ToLowerInvariant());
                var upper = (uint)MurMur3.HashData(path.ToUpperInvariant());
                return ((ulong)upper << 32) | lower;
            }
        }
    }
}
