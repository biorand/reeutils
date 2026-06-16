using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REEUtils;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IntelOrca.Biohazard.REEUtils.Tools
{
    [McpServerToolType]
    internal sealed class PakTools
    {
        [McpServerTool(Name = "search", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("SLOW. Searches file CONTENTS using a regex pattern across matching pak entries. Decompresses and reads file data — expensive. Prefer find + read instead. Only use when you need to grep for specific values inside files (e.g. a GUID or specific field value). Requires set_game to have completed first.")]
        public static string Search(
            [Description("Regex pattern to search for in paths or values.")] string regex,
            [Description("Pak paths, prefixes, or wildcard patterns to search within.")] string[] paths,
            [Description("Maximum number of matches to return.")] int maxResults,
            McpSession session)
        {
            if (string.IsNullOrWhiteSpace(regex))
                throw new McpException("A regex pattern must be specified.");
            if (paths == null || paths.Length == 0)
                throw new McpException("At least one path must be specified.");
            if (maxResults <= 0)
                throw new McpException("max_results must be greater than zero.");

            var pak = session.Pak ?? throw new McpException("No pak is open. Call open_pak first.");
            var pakList = session.PakList ?? throw new McpException("No pak list is loaded. Call set_game first and wait for it to complete before calling search. Do not send tool calls in parallel.");
            var pattern = new Regex(regex, RegexOptions.IgnoreCase);
            var results = new List<SearchResult>();
            var matchedEntries = MatchPakEntries(pakList, paths).ToArray();

            foreach (var entry in matchedEntries)
            {
                if (results.Count >= maxResults)
                    break;

                var data = pak.GetEntryData(entry);
                if (data == null)
                    continue;

                try
                {
                    var searchable = GetSearchableContent(entry, data, session);
                    foreach (Match match in pattern.Matches(searchable))
                    {
                        results.Add(new SearchResult
                        {
                            File = entry,
                            Value = match.Value
                        });
                        if (results.Count >= maxResults)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new SearchResult
                    {
                        File = entry,
                        Error = ex.Message
                    });
                }
            }

            return ToJson(new
            {
                pattern = regex,
                scannedEntries = matchedEntries.Length,
                truncated = results.Count >= maxResults,
                results
            });
        }

        [McpServerTool(Name = "find", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("DISCOVERS file paths inside the open pak using substring or glob pattern matching on filenames. FAST — hash-only lookup, no file reading. Returns full pak-internal paths. Use this to locate a file when you know its name but not its full path, then pass the result to read. Requires set_game to have completed first (call it before this, not in parallel).")]
        public static string Find(
            [Description("One or more path patterns.")] string[] patterns,
            McpSession session)
        {
            if (patterns == null || patterns.Length == 0)
                throw new McpException("At least one pattern must be specified.");

            var pak = session.Pak ?? throw new McpException("No pak is open. Call open_pak first.");
            var pakList = session.PakList ?? throw new McpException("No pak list is loaded. Call set_game first and wait for it to complete before calling find. Do not send tool calls in parallel.");
            var paths = PakPathMatcher.FindMatchingEntries(pak, pakList, patterns);

            return ToJson(new
            {
                patterns,
                paths
            });
        }

        [McpServerTool(Name = "list_files", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Lists files and directories in the open pak (like ls). Use to browse the pak directory structure. For targeted file discovery when you know the filename, prefer find with a specific pattern instead.")]
        public static string ListFiles(
            [Description("Optional pak directory or file path. Leave empty to list the pak root.")] string? path,
            McpSession session)
        {
            var pak = session.Pak ?? throw new McpException("No pak is open. Call open_pak first.");
            var pakList = session.PakList;
            var target = path?.Replace('\\', '/').Trim('/') ?? string.Empty;

            if (!string.IsNullOrEmpty(target) && pakList == null)
                throw new McpException("list_files requires an open pak list when listing directories.");

            if (!string.IsNullOrEmpty(target) && TryResolveExistingFile(session, target, out var filePath))
            {
                return ToJson(new
                {
                    path = filePath,
                    entries = new[]
                    {
                        new
                        {
                            name = Path.GetFileName(filePath),
                            fullPath = filePath,
                            isDirectory = false,
                            size = (long)(pak.GetEntryData(filePath)?.LongLength ?? 0)
                        }
                    }
                });
            }

            if (pakList == null)
            {
                return ToJson(new
                {
                    path = string.Empty,
                    entries = pak.FileHashes.Select(x => new
                    {
                        name = x.ToString("X16"),
                        fullPath = x.ToString("X16"),
                        isDirectory = false,
                        size = (long?)null
                    }).ToArray()
                });
            }

            var prefix = string.IsNullOrEmpty(target) ? string.Empty : target + "/";
            var existingHashes = new HashSet<ulong>(pak.FileHashes);
            var children = new Dictionary<string, ChildEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in pakList.Entries)
            {
                if (!string.IsNullOrEmpty(prefix) &&
                    !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ulong hash;
                try
                {
                    hash = PakPathMatcher.ComputeNormalizedPathHash(name);
                }
                catch
                {
                    continue;
                }

                if (!existingHashes.Contains(hash))
                    continue;

                var relative = !string.IsNullOrEmpty(prefix) ? name.Substring(prefix.Length) : name;
                if (string.IsNullOrEmpty(relative))
                    continue;

                var slashIndex = relative.IndexOf('/');
                var childName = slashIndex == -1 ? relative : relative.Substring(0, slashIndex);
                var isDirectory = slashIndex != -1;

                if (!children.TryGetValue(childName, out var child))
                {
                    child = new ChildEntry
                    {
                        Name = childName,
                        FullPath = isDirectory ? prefix + childName : name,
                        IsDirectory = isDirectory
                    };
                    children.Add(childName, child);
                }
                else if (isDirectory)
                {
                    child.IsDirectory = true;
                    child.FullPath = prefix + childName;
                }

                if (!isDirectory)
                {
                    child.Size = pak.GetEntryData(name)?.LongLength;
                }
            }

            return ToJson(new
            {
                path = target,
                entries = children.Values
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new
                    {
                        name = x.Name,
                        fullPath = x.FullPath,
                        isDirectory = x.IsDirectory,
                        size = x.Size
                    })
                    .ToArray()
            });
        }

        [McpServerTool(Name = "read", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Reads a file from the open pak by its full pak-internal path and returns its contents as JSON (for .msg, .user, .scn, .pfb files). Call find first to discover the correct full path. Requires set_game to have completed first.")]
        public static string Read(
            [Description("A disk path or pak-internal path to read.")] string path,
            McpSession session,
            [Description("Scene node paths whose components should be expanded.")] string[]? xpaths = null,
            [Description("When true, expands all scene components.")] bool full = false)
        {
            var data = session.ReadFileData(path, out var resolvedPath);
            try
            {
                var handler = FileHandlerFactory.Default.Create(resolvedPath, data, session.RszTypeRepository);
                if (handler.RequiresTypeRepository && session.RszTypeRepository == null)
                    throw new McpException("No RSZ repository is loaded. Call open_rsz or set_game first.");

                using var json = handler.GetJson(new TreeOptions
                {
                    Xpaths = xpaths ?? [],
                    Full = full
                });
                return JsonSupport.ToJsonString(json);
            }
            catch (NotSupportedException ex)
            {
                throw new McpException(ex.Message);
            }
        }

        private static string GetSearchableContent(string path, byte[] data, McpSession session)
        {
            try
            {
                var handler = FileHandlerFactory.Default.Create(path, data, session.RszTypeRepository);
                if (handler.RequiresTypeRepository && session.RszTypeRepository == null)
                    return System.Text.Encoding.UTF8.GetString(data);

                using var json = handler.GetJson(TreeOptions.Root);
                return JsonSupport.ToJsonString(json);
            }
            catch (NotSupportedException)
            {
                return System.Text.Encoding.UTF8.GetString(data);
            }
        }

        private static string ToJson(object value)
        {
            return System.Text.Json.JsonSerializer.Serialize(value, JsonSupport.CreateOptions(camelCase: true));
        }

        private static IEnumerable<string> MatchPakEntries(PakList pakList, IReadOnlyList<string> paths)
        {
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                var containsWildcard = path.Contains('*') || path.Contains('?');
                if (containsWildcard)
                {
                    var rx = new Regex("^" + Regex.Escape(path).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
                    foreach (var entry in pakList.Entries)
                    {
                        if (rx.IsMatch(entry))
                            matched.Add(entry);
                    }
                    continue;
                }

                var exact = pakList.Entries.FirstOrDefault(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    matched.Add(exact);
                    continue;
                }

                var full = FileHandlerFactory.Default.GetFullPathFromArg(path);
                exact = pakList.Entries.FirstOrDefault(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    matched.Add(exact);
                    continue;
                }

                exact = pakList.Entries.FirstOrDefault(x => string.Equals(FileHandlerFactory.Default.GetReferencePath(x), path, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    matched.Add(exact);
                    continue;
                }

                var prefix = path;
                if (prefix.StartsWith("natives/stm/", StringComparison.OrdinalIgnoreCase))
                    prefix = prefix.Substring("natives/stm/".Length);
                else if (prefix.StartsWith("natives/stm", StringComparison.OrdinalIgnoreCase))
                    prefix = prefix.Substring("natives/stm".Length).TrimStart('/', '\\');

                foreach (var entry in pakList.Entries)
                {
                    var entryRef = FileHandlerFactory.Default.GetReferencePath(entry);
                    if (entryRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        matched.Add(entry);
                }
            }

            return matched;
        }

        private static bool TryResolveExistingFile(McpSession session, string path, out string resolvedPath)
        {
            try
            {
                resolvedPath = session.ResolvePakPath(path);
                return true;
            }
            catch (McpException)
            {
                resolvedPath = "";
                return false;
            }
        }

        private sealed class ChildEntry
        {
            public required string Name { get; init; }
            public required string FullPath { get; set; }
            public bool IsDirectory { get; set; }
            public long? Size { get; set; }
        }

        private sealed class SearchResult
        {
            public string File { get; init; } = "";
            public string? Value { get; init; }
            public string? Error { get; init; }
        }
    }
}
