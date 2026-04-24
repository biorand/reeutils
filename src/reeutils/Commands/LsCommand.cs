using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class LsCommand : AsyncCommand<LsCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--pak")]
            public string? PakPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [CommandOption("-i|--install")]
            public string? InstallPath { get; init; }

            [CommandArgument(0, "<path>")]
            public string? Path { get; init; }

            [CommandOption("-l|--long")]
            public bool Long { get; init; }

            [CommandOption("-h|--human")]
            public bool HumanReadable { get; init; }

            [CommandOption("-a|--all")]
            public bool All { get; init; }
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (string.IsNullOrEmpty(settings.PakPath) && string.IsNullOrEmpty(settings.Game))
            {
                AnsiConsole.MarkupLine("[red]Either --pak <file> or -g <game> (with --install) must be specified.[/]");
                return Task.FromResult(1);
            }

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
            if (!string.IsNullOrEmpty(settings.Game))
            {
                pakList = EmbeddedData.GetPakList(settings.Game);
                if (pakList == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]Pak list for game '{settings.Game}' not found in embedded data. Names will be shown as hashes.[/]");
                }
            }

            var target = settings.Path ?? string.Empty;
            var targetNormalized = target.Trim('/');
            var prefix = string.IsNullOrEmpty(targetNormalized) ? string.Empty : targetNormalized + "/";

            var children = new Dictionary<string, (bool IsDir, List<ulong> Hashes)>(StringComparer.OrdinalIgnoreCase);

            if (pakList != null)
            {
                // Build a hash set of entries that actually exist in the pak for O(1) lookup.
                var existingHashes = new HashSet<ulong>(pak.FileHashes);

                foreach (var name in pakList.Entries)
                {
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (!settings.All && !string.IsNullOrEmpty(prefix) &&
                        !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Only consider entries that are present in the pak
                    ulong hash;
                    try
                    {
                        hash = ComputeNormalizedPathHash(name);
                    }
                    catch
                    {
                        continue;
                    }

                    if (!existingHashes.Contains(hash))
                        continue;

                    string rel = !string.IsNullOrEmpty(prefix) ? name.Substring(prefix.Length) : name;
                    if (string.IsNullOrEmpty(rel))
                        continue;

                    var slashIndex = rel.IndexOf('/');
                    string childName;
                    bool isDir = false;
                    if (slashIndex == -1)
                    {
                        childName = rel;
                        isDir = false;
                    }
                    else
                    {
                        childName = rel.Substring(0, slashIndex);
                        isDir = true;
                    }

                    if (!children.TryGetValue(childName, out var existing))
                    {
                        existing = (isDir, new List<ulong>());
                        children[childName] = existing;
                    }
                    else if (isDir && !existing.IsDir)
                    {
                        existing.IsDir = true;
                        children[childName] = existing;
                    }

                    if (!isDir)
                        existing.Hashes.Add(hash);
                }
            }
            else
            {
                // Fallback: list hashes as names if no paklist
                foreach (var hash in pak.FileHashes)
                {
                    var name = hash.ToString("X16");
                    children[name] = (false, new List<ulong> { hash });
                }
            }

            var ordered = children.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();

            if (settings.Long)
            {
                var table = new Table();
                table.Border = TableBorder.None;
                table.ShowHeaders = false;
                table.AddColumn(new TableColumn("Type"));
                table.AddColumn(new TableColumn("Size").RightAligned());
                table.AddColumn(new TableColumn("Name"));

                foreach (var kv in ordered)
                {
                    var childName = kv.Key;
                    var isDir = kv.Value.IsDir;
                    var hashes = kv.Value.Hashes;

                    var sizeText = string.Empty;
                    if (!isDir && hashes.Count > 0)
                    {
                        long size = 0;
                        try
                        {
                            var data = pak.GetEntryData(hashes[0]);
                            if (data != null) size = data.LongLength;
                        }
                        catch { }
                        sizeText = settings.HumanReadable ? HumanReadableSize(size) : size.ToString();
                    }

                    if (isDir)
                    {
                        table.AddRow("[bold]d[/]", string.Empty, $"[bold cyan]{childName}[/]");
                    }
                    else
                    {
                        table.AddRow("-", sizeText, childName);
                    }
                }

                AnsiConsole.Write(table);
            }
            else
            {
                // Space-separated listing (wrap lines to console width)
                int consoleWidth = 80;
                try { consoleWidth = Console.WindowWidth; } catch { }
                int spacing = 2;
                int cur = 0;
                foreach (var kv in ordered)
                {
                    var childName = kv.Key;
                    var isDir = kv.Value.IsDir;
                    string formatted = isDir ? $"[bold cyan]{childName}[/]" : childName;
                    int len = childName.Length;
                    if (cur > 0 && cur + spacing + len > consoleWidth)
                    {
                        AnsiConsole.WriteLine("");
                        cur = 0;
                    }
                    if (cur > 0)
                    {
                        AnsiConsole.Write(new string(' ', spacing));
                        cur += spacing;
                    }
                    AnsiConsole.Markup(formatted);
                    cur += len;
                }
                AnsiConsole.WriteLine("");
            }

            return Task.FromResult(0);
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

        private static string HumanReadableSize(long size)
        {
            if (size < 1024) return size + "B";
            double s = size;
            string[] units = { "B", "K", "M", "G", "T" };
            int u = 0;
            while (s >= 1024 && u < units.Length - 1)
            {
                s /= 1024.0;
                u++;
            }
            return $"{s:F1}{units[u]}";
        }
    }
}
