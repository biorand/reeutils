using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class TreeCommand : AsyncCommand<TreeCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("File path (on disk or in pak)")]
            [CommandOption("--path")]
            public string? PathOption { get; init; }

            [Description("Pak file path")]
            [CommandOption("--pak")]
            public string? PakPath { get; init; }

            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [CommandArgument(0, "[args...]")]
            public string[] Args { get; init; } = Array.Empty<string>();
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            var path = GetFilePath(settings);
            if (string.IsNullOrEmpty(path))
            {
                return ValidationResult.Error("File path not specified. Use --path <path> or provide it as the first argument.");
            }
            if (!string.IsNullOrEmpty(settings.PakPath) && !File.Exists(settings.PakPath))
            {
                return ValidationResult.Error($"{settings.PakPath} not found");
            }
            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var path = GetFilePath(settings);
            var filters = GetFilters(settings);

            byte[]? fileData;
            if (!string.IsNullOrEmpty(settings.PakPath))
            {
                var pak = new PakFile(settings.PakPath);
                fileData = pak.GetEntryData(path!);
                if (fileData == null)
                {
                    AnsiConsole.MarkupLine($"[red]File {path} not found in pak.[/]");
                    return ExitCodes.FileNotFound;
                }
            }
            else if (File.Exists(path))
            {
                fileData = await File.ReadAllBytesAsync(path);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]File {path} not found.[/]");
                return ExitCodes.FileNotFound;
            }

            if (settings.Game == null)
            {
                AnsiConsole.MarkupLine("[red]Game not specified. Use -g <game>.[/]");
                return ExitCodes.Help;
            }

            var repo = GetRszTypeRepository(settings.Game);
            if (repo == null)
            {
                AnsiConsole.MarkupLine($"[red]{settings.Game} not recognized.[/]");
                return ExitCodes.Help;
            }

            var (extension, version) = GetRealExtension(path!);

            if (extension == ".user")
            {
                var userFile = new UserFile(fileData);
                var objects = userFile.GetObjects(repo);
                PrintUserTree(objects, filters);
            }
            else if (extension == ".scn")
            {
                var scnFile = new ScnFile(version, fileData);
                var scene = scnFile.ReadScene(repo);
                PrintSceneTree(scene, filters, scnFile.Resources);
            }
            else if (extension == ".pfb")
            {
                var pfbFile = new PfbFile(version, fileData);
                var scene = pfbFile.ReadScene(repo);
                PrintSceneTree(scene, filters, pfbFile.Resources);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Unsupported file format: {extension}[/]");
                return ExitCodes.Help;
            }

            return 0;
        }

        private static string? GetFilePath(Settings settings)
        {
            if (!string.IsNullOrEmpty(settings.PathOption))
                return settings.PathOption;
            if (!string.IsNullOrEmpty(settings.PakPath) && settings.Args.Length > 0)
                return settings.Args[0];
            if (settings.Args.Length > 0)
                return settings.Args[0];
            return null;
        }

        private static string[] GetFilters(Settings settings)
        {
            if (!string.IsNullOrEmpty(settings.PakPath) && !string.IsNullOrEmpty(settings.PathOption))
            {
                // --pak and --path both set, all args are filters
                return settings.Args;
            }
            if (!string.IsNullOrEmpty(settings.PakPath))
            {
                // When using --pak without --path, first arg is the pak-internal path, rest are filters
                if (settings.Args.Length > 1)
                    return settings.Args[1..];
                return Array.Empty<string>();
            }
            if (!string.IsNullOrEmpty(settings.PathOption))
                return settings.Args;
            if (settings.Args.Length > 1)
                return settings.Args[1..];
            return Array.Empty<string>();
        }

        private static void PrintUserTree(ImmutableArray<RszObjectNode> objects, string[] filters)
        {
            var root = new Tree("");
            foreach (var obj in objects)
            {
                PrintObjectNode(root, obj, filters.Any());
            }
            AnsiConsole.Write(root);
        }

        private static void PrintSceneTree(RszScene scene, string[] filters, ImmutableArray<string> resources)
        {
            var root = new Tree("");
            var hasFilters = filters.Any();
            var filterSet = filters.Select(f => f.TrimEnd('/')).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var child in scene.Children)
            {
                PrintSceneNode(root, child, "", hasFilters, filterSet, false);
            }
            AnsiConsole.Write(root);
        }

        private static void PrintSceneNode(IHasTreeNodes parent, IRszSceneNode node, string currentPath, bool hasFilters, HashSet<string> filterSet, bool forceShow)
        {
            if (node is RszFolder folder)
            {
                var externalPath = GetFolderExternalPath(folder);
                var folderName = folder.Name;
                var newPath = string.IsNullOrEmpty(currentPath) ? folderName : $"{currentPath}/{folderName}";
                var match = MatchesFilter(newPath, filterSet) || MatchesFilter(folderName, filterSet);
                var show = !hasFilters || forceShow || match || AnyChildMatches(node, newPath, filterSet);
                if (!show) return;

                var label = string.IsNullOrEmpty(externalPath)
                    ? $"[lightskyblue1]{SafeMarkup(folderName)}/[/]"
                    : $"[lightskyblue1]{SafeMarkup(folderName)}/[/] [dim](external: {SafeMarkup(externalPath)})[/]";
                var treeNode = parent.AddNode(label);

                foreach (var child in folder.Children)
                {
                    PrintSceneNode(treeNode, child, newPath, hasFilters, filterSet, forceShow || match);
                }
            }
            else if (node is RszGameObject gameObject)
            {
                var goName = gameObject.Name;
                var goGuid = gameObject.Guid.ToString();
                var newPath = string.IsNullOrEmpty(currentPath) ? goName : $"{currentPath}/{goName}";
                var match = MatchesFilter(newPath, filterSet) || MatchesFilter(goName, filterSet) || MatchesFilter(goGuid, filterSet);
                var show = !hasFilters || forceShow || match || AnyChildMatches(node, newPath, filterSet);
                if (!show) return;

                var componentList = gameObject.Components
                    .Where(c => c.Type.Name != "via.Transform")
                    .Select(c => c.Type.Name)
                    .ToList();

                var componentSummary = GetComponentSummary(componentList);
                var label = string.IsNullOrEmpty(componentSummary)
                    ? $"[white]{SafeMarkup(goName)}[/] [green]{goGuid}[/]"
                    : $"[white]{SafeMarkup(goName)}[/] [green]{goGuid}[/] {componentSummary}";
                var treeNode = parent.AddNode(label);

                if (match)
                {
                    // Show components with properties for explicitly matched nodes only
                    foreach (var component in gameObject.Components)
                    {
                        PrintComponentNode(treeNode, component);
                    }
                }

                foreach (var child in gameObject.Children)
                {
                    PrintSceneNode(treeNode, child, newPath, hasFilters, filterSet, forceShow || match);
                }
            }
        }

        private static void PrintComponentNode(IHasTreeNodes parent, RszObjectNode component)
        {
            var compLabel = $"[orange1]{{{SafeMarkup(component.Type.Name)}}}[/]";
            var compNode = parent.AddNode(compLabel);
            PrintObjectFields(compNode, component);
        }

        private static void PrintObjectNode(IHasTreeNodes parent, RszObjectNode node, bool isFilterMode)
        {
            var label = $"[white]{SafeMarkup(node.Type.Name)}[/]";
            var treeNode = parent.AddNode(label);
            PrintObjectFields(treeNode, node);
        }

        private static void PrintObjectFields(IHasTreeNodes parent, RszObjectNode node)
        {
            for (var i = 0; i < node.Children.Length; i++)
            {
                var field = node.Type.Fields[i];
                var child = node.Children[i];
                PrintFieldNode(parent, field.Name, child);
            }
        }

        private static void PrintFieldNode(IHasTreeNodes parent, string fieldName, IRszNode node)
        {
            if (node is RszObjectNode objectNode)
            {
                var label = $"[white]{SafeMarkup(fieldName)}[/]";
                var treeNode = SafeAddNode(parent, label);
                PrintObjectFields(treeNode, objectNode);
            }
            else if (node is RszArrayNode arrayNode)
            {
                var lengthStr = arrayNode.Length.ToString();
                var label = $"[white]{SafeMarkup(fieldName)}[/] [grey]{lengthStr}[/]";
                var treeNode = SafeAddNode(parent, label);
                for (var i = 0; i < arrayNode.Length; i++)
                {
                    PrintFieldNode(treeNode, $"[{i}]", arrayNode[i]);
                }
            }
            else
            {
                var valueStr = FormatValue(node);
                var label = $"[white]{SafeMarkup(fieldName)}[/] = [green]{SafeMarkup(valueStr)}[/]";
                SafeAddNode(parent, label);
            }
        }

        private static IHasTreeNodes SafeAddNode(IHasTreeNodes parent, string label)
        {
            try
            {
                return parent.AddNode(label);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Markup error in label: '{label}'");
                Console.Error.WriteLine($"Exception: {ex.Message}");
                throw;
            }
        }

        private static string FormatValue(IRszNode node)
        {
            if (node is RszStringNode s) return s.Value ?? "null";
            if (node is RszResourceNode r) return r.Value ?? "null";
            if (node is RszUserDataNode u) return $"{u.Path ?? "null"} ({u.Type?.Name ?? "?"})";
            if (node is RszNullNode) return "null";
            if (node is RszValueNode v)
            {
                var value = RszSerializer.Deserialize(v);
                return value switch
                {
                    Vector2 vec2 => $"<{vec2.X}, {vec2.Y}>",
                    Vector3 vec3 => $"<{vec3.X}, {vec3.Y}, {vec3.Z}>",
                    Vector4 vec4 => $"<{vec4.X}, {vec4.Y}, {vec4.Z}, {vec4.W}>",
                    Quaternion q => $"<{q.X}, {q.Y}, {q.Z}, {q.W}>",
                    Guid g => g.ToString(),
                    bool b => b.ToString(),
                    _ => value?.ToString() ?? "null"
                };
            }
            return node.ToString() ?? "?";
        }

        private static string GetComponentSummary(List<string> componentList)
        {
            if (componentList.Count == 0) return "";
            var limited = componentList.Take(3).Select(SafeMarkup).ToList();
            var summary = string.Join(", ", limited);
            if (componentList.Count > 3)
                summary += ", ...";
            return $"[orange1]{{{summary}}}[/]";
        }

        private static string? GetFolderExternalPath(RszFolder folder)
        {
            try
            {
                var settings = folder.Settings;
                if (settings.Type.FindFieldIndex("ScenePath") != -1)
                {
                    if (settings["ScenePath"] is RszResourceNode resourceNode && !string.IsNullOrEmpty(resourceNode.Value))
                    {
                        return resourceNode.Value;
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool MatchesFilter(string value, HashSet<string> filterSet)
        {
            if (!filterSet.Any()) return false;
            foreach (var filter in filterSet)
            {
                if (value.Equals(filter, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool AnyChildMatches(IRszSceneNode node, string currentPath, HashSet<string> filterSet)
        {
            foreach (var child in node.Children)
            {
                if (child is RszFolder folder)
                {
                    var newPath = string.IsNullOrEmpty(currentPath) ? folder.Name : $"{currentPath}/{folder.Name}";
                    if (MatchesFilter(newPath, filterSet) || MatchesFilter(folder.Name, filterSet) || AnyChildMatches(child, newPath, filterSet))
                        return true;
                }
                else if (child is RszGameObject gameObject)
                {
                    var newPath = string.IsNullOrEmpty(currentPath) ? gameObject.Name : $"{currentPath}/{gameObject.Name}";
                    if (MatchesFilter(newPath, filterSet) || MatchesFilter(gameObject.Name, filterSet) || MatchesFilter(gameObject.Guid.ToString(), filterSet))
                        return true;
                    if (AnyChildMatches(child, newPath, filterSet))
                        return true;
                }
            }
            return false;
        }

        private static string SafeMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return Markup.Escape(text);
        }

        private static RszTypeRepository? GetRszTypeRepository(string game)
        {
            var rszJsonGz = EmbeddedData.GetFile($"rsz{game}.json.gz");
            if (rszJsonGz != null)
                return RszRepositorySerializer.Default.FromJsonGz(rszJsonGz);

            var rszJson = EmbeddedData.GetCompressedFile($"rsz{game}.json");
            if (rszJson != null)
                return RszRepositorySerializer.Default.FromJson(rszJson);

            return null;
        }

        private static (string Extension, int Version) GetRealExtension(string path)
        {
            var extension = Path.GetExtension(path);
            if (int.TryParse(extension.AsSpan(1), out var version))
            {
                return (Path.GetExtension(Path.GetFileNameWithoutExtension(path)).ToLowerInvariant(), version);
            }
            return (Path.GetExtension(path).ToLowerInvariant(), 0);
        }
    }
}
