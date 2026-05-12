using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class GrepCommand : AsyncCommand<GrepCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Input pak file or directory")]
            [CommandOption("-p|--pak")]
            public required string Pak { get; init; }

            [Description("Regex pattern to search for")]
            [CommandOption("-r|--regex")]
            public required string Pattern { get; init; }

            [Description("Game identifier for embedded pak lists/rsz")]
            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [Description("Pak list path")]
            [CommandOption("-l|--pak-list")]
            public string? PakListPath { get; init; }

            [Description("Enable debug output")]
            [CommandOption("-d|--debug")]
            public bool Debug { get; init; }

            [CommandArgument(0, "[paths..]")]
            public string[] Paths { get; init; } = Array.Empty<string>();
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.Pak) && !Directory.Exists(settings.Pak))
            {
                return ValidationResult.Error($"{settings.Pak} not found");
            }
            if (settings.PakListPath != null && !File.Exists(settings.PakListPath))
            {
                return ValidationResult.Error($"{settings.PakListPath} not found");
            }
            if (string.IsNullOrEmpty(settings.Pattern))
            {
                return ValidationResult.Error("Regex pattern not specified");
            }
            if (settings.Paths == null || settings.Paths.Length == 0)
            {
                return ValidationResult.Error("At least one path must be specified");
            }
            if (settings.PakListPath == null && settings.Game == null)
            {
                return ValidationResult.Error($"A game or pak list must be specified.");
            }
            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            IPakFile pakFile;
            if (File.Exists(settings.Pak))
            {
                pakFile = new PakFile(settings.Pak);
            }
            else
            {
                pakFile = new RePakCollection(settings.Pak);
            }

            var pakList = settings.PakListPath == null
                ? EmbeddedData.GetPakList(settings.Game!) ?? throw new Exception($"{settings.Game} not recognized.")
                : new PakList(File.ReadAllText(settings.PakListPath));

            var repo = settings.Game == null ? null : GetRszTypeRepository(settings.Game!);

            if (settings.Debug)
            {
                Console.Error.WriteLine($"DEBUG: pak list entries: {pakList.Entries.Length}");
                Console.Error.WriteLine($"DEBUG: rsz repo found: { (repo != null ? "yes" : "no") }");
            }

            var patterns = settings.Paths;
            var matched = new List<string>();

            foreach (var p in patterns)
            {
                var containsWildcard = p.Contains('*') || p.Contains('?');
                if (containsWildcard)
                {
                    var rx = new Regex("^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
                    foreach (var entry in pakList.Entries)
                    {
                        if (rx.IsMatch(entry))
                            matched.Add(entry);
                    }
                }
                else
                {
                    // Normalize argument for reference matching
                    var arg = p;
                    if (arg.StartsWith("natives/stm/", StringComparison.OrdinalIgnoreCase))
                        arg = arg.Substring("natives/stm/".Length);
                    else if (arg.StartsWith("natives/stm", StringComparison.OrdinalIgnoreCase))
                        arg = arg.Substring("natives/stm".Length).TrimStart('/', '\\');

                    // Exact match attempt (case-insensitive)
                    var exact = pakList.Entries.FirstOrDefault(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase));
                    if (exact != null)
                    {
                        matched.Add(exact);
                        continue;
                    }

                    // Try full path conversion
                    var full = GetFullPathFromArg(p);
                    exact = pakList.Entries.FirstOrDefault(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase));
                    if (exact != null)
                    {
                        matched.Add(exact);
                        continue;
                    }

                    // Try matching by reference path equality
                    exact = pakList.Entries.FirstOrDefault(x => string.Equals(GetReferencePath(x), p, StringComparison.OrdinalIgnoreCase));
                    if (exact != null)
                    {
                        matched.Add(exact);
                        continue;
                    }

                    // Prefix match: treat the argument as a directory prefix and match any entries whose reference path starts with it
                    foreach (var entry in pakList.Entries)
                    {
                        var entryRef = GetReferencePath(entry);
                        if (entryRef.StartsWith(arg, StringComparison.OrdinalIgnoreCase))
                        {
                            matched.Add(entry);
                        }
                    }
                }
            }

            matched = matched.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (settings.Debug)
            {
                Console.Error.WriteLine($"DEBUG: matched entries: {matched.Count}");
                var idx = 0;
                foreach (var m in matched.Take(20))
                {
                    Console.Error.WriteLine($"DEBUG: match[{idx++}]: {m}");
                }
            }

            var patternRegex = new Regex(settings.Pattern, RegexOptions.IgnoreCase);
            var sync = new object();

            foreach (var entry in matched)
            {
                try
                {
                    var data = pakFile.GetEntryData(entry);
                    if (data == null)
                        continue;

                    var searchable = GetSearchableContent(entry, data, repo);
                    foreach (Match match in patternRegex.Matches(searchable))
                    {
                        lock (sync) Console.WriteLine($"{entry}: {match.Value}");
                    }
                }
                catch (Exception e)
                {
                    lock (sync)
                    {
                        var backup = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(entry + " : " + e.Message);
                        Console.ForegroundColor = backup;
                    }
                }
            }

            return 0;

            // Local helpers
            static string GetReferencePath(string path)
            {
                return FileHandlerFactory.Default.GetReferencePath(path);
            }

            static string GetFullPathFromArg(string path)
            {
                return FileHandlerFactory.Default.GetFullPathFromArg(path);
            }

            static RszTypeRepository? GetRszTypeRepository(string game)
            {
                var rszJsonGz = EmbeddedData.GetFile($"rsz{game}.json.gz");
                if (rszJsonGz != null)
                    return RszRepositorySerializer.Default.FromJsonGz(rszJsonGz);

                var rszJson = EmbeddedData.GetCompressedFile($"rsz{game}.json");
                if (rszJson != null)
                    return RszRepositorySerializer.Default.FromJson(rszJson);

                return null;
            }

            static string GetSearchableContent(string entry, byte[] data, RszTypeRepository? repo)
            {
                try
                {
                    var handler = FileHandlerFactory.Default.Create(entry, data, repo);
                    if (handler.RequiresTypeRepository && repo == null)
                        return System.Text.Encoding.UTF8.GetString(data);

                    using var json = handler.GetJson(TreeOptions.Root);
                    return JsonSupport.ToJsonString(json);
                }
                catch (NotSupportedException)
                {
                    return System.Text.Encoding.UTF8.GetString(data);
                }
            }
        }
    }
}
