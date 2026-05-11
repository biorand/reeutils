using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using ModelContextProtocol;

namespace IntelOrca.Biohazard.REEUtils
{
    internal static class McpServerSupport
    {
        public static string ListFiles(McpSession session, string? path)
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
                    hash = ComputeNormalizedPathHash(name);
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

        public static string Search(McpSession session, string regex, IReadOnlyList<string> paths, int maxResults)
        {
            if (string.IsNullOrWhiteSpace(regex))
                throw new McpException("A regex pattern must be specified.");
            if (paths == null || paths.Count == 0)
                throw new McpException("At least one path must be specified.");
            if (maxResults <= 0)
                throw new McpException("max_results must be greater than zero.");

            var pak = session.Pak ?? throw new McpException("No pak is open. Call open_pak first.");
            var pakList = session.PakList ?? throw new McpException("No pak list is loaded. Call open_pak_list or set_game first.");
            var repo = session.RszTypeRepository;
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

                var (extension, version) = GetRealExtension(entry);
                try
                {
                    if (extension == ".user" && repo != null)
                    {
                        foreach (var obj in new UserFile(data).GetObjects(repo))
                        {
                            VisitRszNode(obj, obj.Type.Name, pattern, entry, results, maxResults);
                            if (results.Count >= maxResults)
                                break;
                        }
                    }
                    else if (extension == ".scn" && repo != null)
                    {
                        var scene = new ScnFile(version, data).ReadScene(repo);
                        VisitRszNode(scene, string.Empty, pattern, entry, results, maxResults);
                    }
                    else if (extension == ".pfb" && repo != null)
                    {
                        var scene = new PfbFile(version, data).ReadScene(repo);
                        VisitRszNode(scene, string.Empty, pattern, entry, results, maxResults);
                    }
                    else
                    {
                        var text = System.Text.Encoding.UTF8.GetString(data);
                        foreach (Match match in pattern.Matches(text))
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

        public static string Read(McpSession session, string path)
        {
            var data = session.ReadFileData(path, out var resolvedPath);
            var (extension, version) = GetRealExtension(resolvedPath);

            if (extension == ".msg")
            {
                var msg = new MsgFile(data).ToBuilder();
                var output = new SerializableMsg
                {
                    Version = msg.Version,
                    Languages = [.. msg.Languages.Cast<int>()],
                    Entries = [.. msg.Messages.Select(x => new SerializableMsg.Entry
                    {
                        Guid = x.Guid,
                        Name = x.Name,
                        Values = [.. x.Values.Select(v => v.Text)]
                    })]
                };
                return output.ToJson(camelCase: true);
            }

            var repo = session.RszTypeRepository ?? throw new McpException("No RSZ repository is loaded. Call open_rsz or set_game first.");
            if (extension == ".user")
            {
                var objects = new UserFile(data).GetObjects(repo);
                if (objects.Length == 1)
                    return RszJsonSerializer.Serialize(objects[0], CreateJsonOptions());

                var documents = objects
                    .Select(x => JsonDocument.Parse(RszJsonSerializer.Serialize(x, CreateJsonOptions())).RootElement.Clone())
                    .ToArray();
                return ToJson(documents);
            }
            if (extension == ".scn")
            {
                var scene = new ScnFile(version, data).ReadScene(repo);
                return RszJsonSerializer.Serialize(scene, CreateJsonOptions());
            }
            if (extension == ".pfb")
            {
                var scene = new PfbFile(version, data).ReadScene(repo);
                return RszJsonSerializer.Serialize(scene, CreateJsonOptions());
            }

            throw new McpException($"Unsupported file format '{extension}'. read currently supports .msg, .user, .scn, and .pfb files.");
        }

        public static string GenerateClass(McpSession session, IReadOnlyList<string> typeNames, bool includeEnums)
        {
            if (typeNames == null || typeNames.Count == 0)
                throw new McpException("At least one type name must be specified.");

            var repo = session.RszTypeRepository ?? throw new McpException("No RSZ repository is loaded. Call open_rsz or set_game first.");
            var types = typeNames.Select(x => ResolveType(repo, x)).ToArray();

            var writer = new RszTypeCsharpWriter
            {
                GenerateEnums = includeEnums
            };
            return writer.Generate(types);
        }

        public static string GetTypeDetails(McpSession session, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new McpException("A type name must be specified.");

            var repo = session.RszTypeRepository ?? throw new McpException("No RSZ repository is loaded. Call open_rsz or set_game first.");
            var type = ResolveType(repo, typeName);
            var nestedTypes = repo.GetNestedTypes(type);

            return ToJson(new
            {
                name = type.Name,
                id = $"0x{type.Id:X8}",
                crc = $"0x{type.Crc:X8}",
                @namespace = type.Namespace,
                nameWithoutNamespace = type.NameWithoutNamespace,
                declaringType = type.DeclaringType?.Name,
                parent = type.Parent?.Name,
                children = type.Children.Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                nestedTypes = nestedTypes.Select(x => x.Name).ToArray(),
                isEnum = type.IsEnum,
                fields = type.Fields.Select(field => new
                {
                    name = field.Name,
                    type = field.Type.ToString(),
                    objectType = field.ObjectType?.Name,
                    isArray = field.IsArray,
                    isNative = field.IsNative,
                    align = field.Align,
                    size = field.Size,
                    isInherited = type.IsFieldInherited(field.Name),
                    propertyId = field.Id
                }).ToArray()
            });
        }

        public static string GetReferencePath(string path)
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

        public static string GetFullPathFromArg(string path)
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

        private static JsonSerializerOptions CreateJsonOptions()
        {
            return new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        private static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value, CreateJsonOptions());
        }

        private static IEnumerable<string> MatchPakEntries(PakList pakList, IReadOnlyList<string> paths)
        {
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in paths)
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
                    continue;
                }

                var exact = pakList.Entries.FirstOrDefault(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    matched.Add(exact);
                    continue;
                }

                var full = GetFullPathFromArg(p);
                exact = pakList.Entries.FirstOrDefault(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    matched.Add(exact);
                    continue;
                }

                exact = pakList.Entries.FirstOrDefault(x => string.Equals(GetReferencePath(x), p, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    matched.Add(exact);
                    continue;
                }

                var prefix = p;
                if (prefix.StartsWith("natives/stm/", StringComparison.OrdinalIgnoreCase))
                    prefix = prefix.Substring("natives/stm/".Length);
                else if (prefix.StartsWith("natives/stm", StringComparison.OrdinalIgnoreCase))
                    prefix = prefix.Substring("natives/stm".Length).TrimStart('/', '\\');

                foreach (var entry in pakList.Entries)
                {
                    var entryRef = GetReferencePath(entry);
                    if (entryRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        matched.Add(entry);
                }
            }

            return matched;
        }

        private static RszType ResolveType(RszTypeRepository repo, string name)
        {
            var type = repo.FromName(name);
            if (type != null)
                return type;

            var suggestions = repo.Types
                .Where(x =>
                    !x.Name.ContainsAny(['[', ']', '<', '>', '`']) &&
                    x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();

            if (suggestions.Length == 1)
                return repo.FromName(suggestions[0])!;

            if (suggestions.Length == 0)
                throw new McpException($"Type '{name}' was not found.");

            throw new McpException($"Type '{name}' was not found. Suggestions: {string.Join(", ", suggestions)}.");
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

        private static ulong ComputeNormalizedPathHash(string path)
        {
            path = path.Replace("\\", "/");
            if (path.Contains("__Unknown"))
            {
                var pathWithoutExtension = Path.GetFileNameWithoutExtension(path);
                return Convert.ToUInt64(pathWithoutExtension, 16);
            }

            var lower = (uint)MurMur3.HashData(path.ToLowerInvariant());
            var upper = (uint)MurMur3.HashData(path.ToUpperInvariant());
            return ((ulong)upper << 32) | lower;
        }

        private static void VisitRszNode(
            IRszNode node,
            string currentPath,
            Regex pattern,
            string entry,
            List<SearchResult> results,
            int maxResults)
        {
            if (results.Count >= maxResults)
                return;

            if (node is RszStringNode stringNode)
            {
                AddMatch(currentPath, stringNode.Value ?? "");
            }
            else if (node is RszValueNode valueNode)
            {
                AddMatch(currentPath, FormatValue(valueNode));
            }
            else if (node is RszResourceNode resourceNode)
            {
                AddMatch(currentPath, resourceNode.Value ?? "");
            }
            else if (node is RszUserDataNode userDataNode)
            {
                AddMatch(currentPath, userDataNode.Path ?? "");
            }
            else if (node is RszArrayNode arrayNode)
            {
                for (var i = 0; i < arrayNode.Children.Length && results.Count < maxResults; i++)
                {
                    VisitRszNode(arrayNode.Children[i], $"{currentPath}[{i}]", pattern, entry, results, maxResults);
                }
            }
            else if (node is RszObjectNode objectNode)
            {
                for (var i = 0; i < objectNode.Children.Length && results.Count < maxResults; i++)
                {
                    var fieldName = objectNode.Type.Fields[i].Name;
                    var path = string.IsNullOrEmpty(currentPath) ? fieldName : $"{currentPath}.{fieldName}";
                    VisitRszNode(objectNode.Children[i], path, pattern, entry, results, maxResults);
                }
            }
            else if (node is RszGameObject gameObject)
            {
                var path = string.IsNullOrEmpty(currentPath) ? gameObject.Name : $"{currentPath}/{gameObject.Name}";
                AddMatch($"{path}.guid", gameObject.Guid.ToString());
                VisitRszNode(gameObject.Settings, path, pattern, entry, results, maxResults);
                for (var i = 0; i < gameObject.Components.Length && results.Count < maxResults; i++)
                {
                    var componentType = gameObject.Components[i]?.Type?.Name ?? "Unknown";
                    VisitRszNode(gameObject.Components[i], $"{path}{{{componentType}}}", pattern, entry, results, maxResults);
                }
                foreach (var child in gameObject.Children)
                {
                    if (results.Count >= maxResults)
                        break;
                    VisitRszNode(child, path + "/" + child.Name, pattern, entry, results, maxResults);
                }
            }
            else if (node is RszFolder folder)
            {
                var path = string.IsNullOrEmpty(currentPath) ? folder.Name : $"{currentPath}/{folder.Name}";
                VisitRszNode(folder.Settings, path, pattern, entry, results, maxResults);
                foreach (var child in folder.Children)
                {
                    if (results.Count >= maxResults)
                        break;
                    VisitRszNode(child, path, pattern, entry, results, maxResults);
                }
            }
            else if (node is RszScene scene)
            {
                foreach (var child in scene.Children)
                {
                    if (results.Count >= maxResults)
                        break;
                    VisitRszNode(child, currentPath, pattern, entry, results, maxResults);
                }
            }
            else if (node is IRszNodeContainer container)
            {
                foreach (var child in container.Children)
                {
                    if (results.Count >= maxResults)
                        break;
                    VisitRszNode(child, currentPath, pattern, entry, results, maxResults);
                }
            }

            void AddMatch(string path, string value)
            {
                if (!pattern.IsMatch(path) && !pattern.IsMatch(value))
                    return;

                results.Add(new SearchResult
                {
                    File = entry,
                    Path = path,
                    Value = value
                });
            }
        }

        private static string FormatValue(RszValueNode valueNode)
        {
            var value = RszSerializer.Deserialize(valueNode);
            return value switch
            {
                Vector2 vec2 => $"<{vec2.X}, {vec2.Y}>",
                Vector3 vec3 => $"<{vec3.X}, {vec3.Y}, {vec3.Z}>",
                Vector4 vec4 => $"<{vec4.X}, {vec4.Y}, {vec4.Z}, {vec4.W}>",
                Quaternion quaternion => $"<{quaternion.X}, {quaternion.Y}, {quaternion.Z}, {quaternion.W}>",
                _ => value?.ToString() ?? "null"
            };
        }

        private sealed class ChildEntry
        {
            public string Name { get; set; } = "";
            public string FullPath { get; set; } = "";
            public bool IsDirectory { get; set; }
            public long? Size { get; set; }
        }

        private sealed class SearchResult
        {
            public string File { get; set; } = "";
            public string? Path { get; set; }
            public string? Value { get; set; }
            public string? Error { get; set; }
        }
    }
}
