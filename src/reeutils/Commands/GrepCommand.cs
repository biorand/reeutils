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

                    // Try full path conversion (e.g. 'spawn/enemy' -> 'natives/stm/spawn/enemy.user.2')
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

            var omitPakPath = settings.Paths.Length == 1 && matched.Count == 1;

            var patternRegex = new Regex(settings.Pattern, RegexOptions.IgnoreCase);
            var sync = new object();

            foreach (var entry in matched)
            {
                try
                {
                    var data = pakFile.GetEntryData(entry);
                    if (data == null)
                        continue;

                    // Handle .user.<version>
                    var userMatch = System.Text.RegularExpressions.Regex.IsMatch(entry, @"\.user\.\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var scnMatch = System.Text.RegularExpressions.Regex.Match(entry, @"\.scn\.(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var pfbMatch = System.Text.RegularExpressions.Regex.Match(entry, @"\.pfb\.(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (userMatch && repo != null)
                    {
                        var userFile = new UserFile(data);
                        var objects = userFile.GetObjects(repo);
                        for (var oi = 0; oi < objects.Length; oi++)
                        {
                            var obj = objects[oi];
                            var basePath = obj.Type.Name; // starting path for this root object
                            VisitRszNode(obj, basePath, (propPath, value) =>
                            {
                                if (patternRegex.IsMatch(propPath) || patternRegex.IsMatch(value))
                                {
                                    var outLine = omitPakPath ? $"{propPath} = {value}" : $"{entry}: {propPath} = {value}";
                                    lock (sync) Console.WriteLine(outLine);
                                }
                            });
                        }
                    }
                    else if (scnMatch.Success && repo != null)
                    {
                        var version = int.Parse(scnMatch.Groups[1].Value);
                        var scn = new ScnFile(version, data).ReadScene(repo);
                        scn.VisitGameObjects(go =>
                        {
                            var goPath = go.Name;
                            VisitRszNode(go, goPath, (propPath, value) =>
                            {
                                if (patternRegex.IsMatch(propPath) || patternRegex.IsMatch(value))
                                {
                                    lock (sync) Console.WriteLine($"{entry}: {propPath} = {value}");
                                }
                            });
                        });
                    }
                    else if (pfbMatch.Success && repo != null)
                    {
                        var version = int.Parse(pfbMatch.Groups[1].Value);
                        var pfb = new PfbFile(version, data);
                        var scene = pfb.ReadScene(repo);
                        scene.VisitGameObjects(go =>
                        {
                            var goPath = go.Name;
                            VisitRszNode(go, goPath, (propPath, value) =>
                            {
                                if (patternRegex.IsMatch(propPath) || patternRegex.IsMatch(value))
                                {
                                    lock (sync) Console.WriteLine($"{entry}: {propPath} = {value}");
                                }
                            });
                        });
                    }
                    else
                    {
                        // Fallback: raw text search
                        var text = System.Text.Encoding.UTF8.GetString(data);
                        foreach (Match m in patternRegex.Matches(text))
                        {
                            lock (sync) Console.WriteLine($"{entry}: {m.Value}");
                        }
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
                if (path.StartsWith("natives/stm", StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(12);
                }
                var extensionIndex = path.LastIndexOf('.');
                if (extensionIndex != -1)
                    path = path.Substring(0, extensionIndex);
                return path;
            }

            static string GetFullPathFromArg(string path)
            {
                if (path.StartsWith("natives/stm", StringComparison.OrdinalIgnoreCase))
                    return path;

                var ender = "";
                if (path.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
                    ender = ".2";
                else if (path.EndsWith(".scn", StringComparison.OrdinalIgnoreCase))
                    ender = ".20";
                else if (path.EndsWith(".pfb", StringComparison.OrdinalIgnoreCase))
                    ender = ".17";
                return "natives/stm/" + path + ender;
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

            static void VisitRszNode(IRszNode node, string currentPath, Action<string, string> onMatch)
            {
                if (node is RszStringNode s)
                {
                    onMatch(currentPath, s.Value ?? "");
                }
                else if (node is RszValueNode v)
                {
                    onMatch(currentPath, v.ToString() ?? "");
                }
                else if (node is RszResourceNode r)
                {
                    onMatch(currentPath, r.Value ?? "");
                }
                else if (node is RszUserDataNode u)
                {
                    onMatch(currentPath, u.Path ?? "");
                }
                else if (node is RszArrayNode a)
                {
                    for (var i = 0; i < a.Children.Length; i++)
                    {
                        var np = $"{currentPath}[{i}]";
                        VisitRszNode(a.Children[i], np, onMatch);
                    }
                }
                else if (node is RszObjectNode o)
                {
                    for (var i = 0; i < o.Children.Length; i++)
                    {
                        var fieldName = o.Type.Fields[i].Name;
                        var np = string.IsNullOrEmpty(currentPath) ? fieldName : $"{currentPath}.{fieldName}";
                        VisitRszNode(o.Children[i], np, onMatch);
                    }
                }
                else if (node is RszGameObject go)
                {
                    var gp = string.IsNullOrEmpty(currentPath) ? go.Name : $"{currentPath}/{go.Name}";
                    // Make GUID searchable
                    onMatch($"{gp}.guid", go.Guid.ToString());
                    // Visit settings
                    VisitRszNode(go.Settings, gp, onMatch);
                    // Visit components, include type in braces for clarity (e.g., GameObject{via.Transform}.Position)
                    for (var i = 0; i < go.Components.Length; i++)
                    {
                        var compType = go.Components[i]?.Type?.Name ?? "Unknown";
                        var cp = $"{gp}{{{compType}}}";
                        VisitRszNode(go.Components[i], cp, onMatch);
                    }
                    // Visit children
                    foreach (var child in go.Children)
                    {
                        VisitRszNode(child, gp + "/" + child.Name, onMatch);
                    }
                }
                else if (node is IRszNodeContainer container)
                {
                    foreach (var child in container.Children)
                    {
                        VisitRszNode(child, currentPath, onMatch);
                    }
                }
            }
        }
    }
}
