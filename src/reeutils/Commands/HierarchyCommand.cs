using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal class HierarchyCommand : AsyncCommand<HierarchyCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Regex pattern")]
            [CommandArgument(0, "<pattern>")]
            public required string Pattern { get; init; }

            [CommandOption("-g|--game")]
            public required string Game { get; init; }

            [CommandOption("-i")]
            public required string InstallDirectory { get; init; }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (string.IsNullOrEmpty(settings.Pattern))
            {
                return ValidationResult.Error($"Pattern not specified");
            }
            if (settings.InstallDirectory == null || !Directory.Exists(settings.InstallDirectory))
            {
                return ValidationResult.Error($"{settings.Pattern} not found");
            }
            return base.Validate(context, settings);
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (settings.Game == null)
                throw new Exception("Game not specified");

            var repo = GetRszTypeRepository(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");
            var pakList = EmbeddedData.GetPakList(settings.Game) ?? throw new Exception($"{settings.Game} not recognized.");

            var pakFile = new RePakCollection(settings.InstallDirectory);
            var dict = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var sync = new object();
            Parallel.ForEach(pakList.Entries, path =>
            {
                try
                {
                    var list = new List<string>();
                    dict[path] = list;
                    if (path.EndsWith(".user.2"))
                    {
                        var root = new UserFile(pakFile.GetEntryData(path)).GetObjects(repo)[0];
                        Explore(list, root);
                    }
                    else if (path.EndsWith(".scn.20"))
                    {
                        var scn = new ScnFile(20, pakFile.GetEntryData(path)).ReadScene(repo);
                        Explore(list, scn);
                    }
                    else if (path.EndsWith(".pfb.17"))
                    {
                        var pfb = new PfbFile(17, pakFile.GetEntryData(path)).ReadScene(repo);
                        Explore(list, pfb);
                    }
                    if (list.Count > 0)
                    {
                        list = list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                }
                catch (Exception e)
                {
                    lock (sync)
                    {
                        var backup = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(path + " : " + e.Message);
                        Console.ForegroundColor = backup;
                    }
                }
            });

            // Create tree
            var nodeMap = dict.ToDictionary(x => x.Key, x => new Node(x.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodeMap.Values)
            {
                var dependencies = dict[node.Path];
                node.Children.AddRange(dependencies.Select(x =>
                {
                    if (!nodeMap.TryGetValue(x, out var n))
                    {
                        n = new Node(x);
                    }
                    n.Parents.Add(node);
                    return n;
                }));
            }

            var roots = nodeMap.Values
                .Where(x => x.Parents.Count == 0 && x.Children.Count != 0)
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var patternRegex = new Regex(settings.Pattern, RegexOptions.IgnoreCase);
            var ansiRoot = new Tree("*");
            foreach (var root in roots)
            {
                PrintTree(ansiRoot, root, 0);
            }
            AnsiConsole.Write(ansiRoot);
            return Task.FromResult(0);

            void PrintTree(IHasTreeNodes ansiNode, Node n, int level)
            {
                if (!n.Contains(patternRegex))
                    return;

                var childAnsiNode = ansiNode.AddNode(n.Path);
                foreach (var child in n.Children)
                {
                    PrintTree(childAnsiNode, child, level + 1);
                }
            }

            void Explore(List<string> dependencies, IRszNode root)
            {
                Visit(root);

                void Visit(IRszNode node)
                {
                    if (node is RszResourceNode resourceNode)
                    {
                        if (!resourceNode.IsEmpty)
                        {
                            AddDependency(resourceNode.Value!);
                        }
                    }
                    else if (node is RszUserDataNode userDataNode)
                    {
                        if (!userDataNode.IsEmpty)
                        {
                            AddDependency(userDataNode.Path!);
                        }
                    }

                    if (node is RszFolder folder)
                    {
                        Visit(folder.Settings);
                    }
                    else if (node is RszGameObject gameObject)
                    {
                        Visit(gameObject.Settings);
                        foreach (var component in gameObject.Components)
                        {
                            Visit(component);
                        }
                    }
                    if (node is IRszNodeContainer container)
                    {
                        foreach (var child in container.Children)
                        {
                            Visit(child);
                        }
                    }
                }

                void AddDependency(string path)
                {
                    var fullPath = PathHelpers.GetFullPath(path);
                    dependencies.Add(fullPath);
                }
            }
        }

        private class Node(string path)
        {
            public string Path => path;
            public List<Node> Parents { get; } = [];
            public List<Node> Children { get; } = [];

            public bool Contains(Regex s)
            {
                if (s.IsMatch(Path))
                    return true;
                foreach (var child in Children)
                {
                    if (child.Contains(s))
                    {
                        return true;
                    }
                }
                return false;
            }

            public override string ToString() => Path;
        }

        private static class PathHelpers
        {
            const string NativesPrefix = "natives/stm/";

            /// <summary>
            /// Gets the path in a format that is used for resource and userdata nodes.
            /// E.g. `natives/stm/spawn/enemy/data/enemy.user.2` becomes `spawn/enemy/data/enemy.user`.
            /// </summary>
            /// <param name="path"></param>
            /// <returns></returns>
            public static string GetReferencePath(string path)
            {
                if (path.StartsWith("natives/stm"))
                {
                    path = path.Substring(12);
                }
                var extensionIndex = path.LastIndexOf('.');
                if (extensionIndex != -1)
                    path = path.Substring(0, extensionIndex);
                return path;
            }

            /// <summary>
            /// Gets the full path to a file in a pak file from the reference path used in a resource or userdata node.
            /// E.g. `spawn/enemy/data/enemy.user` becomes `natives/stm/spawn/enemy/data/enemy.user.2`.
            /// </summary>
            /// <param name="path"></param>
            /// <returns></returns>
            public static string GetFullPath(string path)
            {
                if (path.StartsWith(NativesPrefix, StringComparison.OrdinalIgnoreCase))
                    return path;

                var ender = "";
                if (path.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
                    ender = ".2";
                else if (path.EndsWith(".scn", StringComparison.OrdinalIgnoreCase))
                    ender = ".20";
                else if (path.EndsWith(".pfb", StringComparison.OrdinalIgnoreCase))
                    ender = ".17";
                return NativesPrefix + path + ender;
            }
        }

        private static RszTypeRepository? GetRszTypeRepository(string game)
        {
            var rszJsonGz = EmbeddedData.GetCompressedFile($"rsz{game}.json");
            if (rszJsonGz == null)
                return null;

            return RszRepositorySerializer.Default.FromJson(rszJsonGz);
        }
    }
}
