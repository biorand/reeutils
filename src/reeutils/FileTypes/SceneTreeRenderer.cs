using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal static class SceneTreeRenderer
    {
        public static Tree CreateTree(RszScene scene, TreeOptions options)
        {
            var root = new Tree("");
            var xpaths = GetXpaths(options);
            foreach (var child in scene.Children)
            {
                PrintSceneNode(root, child, "", xpaths, options.Full);
            }
            return root;
        }

        public static IRszNode SelectNode(RszScene scene, string xpath)
        {
            var normalized = NormalizePath(xpath);
            if (string.IsNullOrWhiteSpace(normalized))
                return scene;

            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
                return scene;

            foreach (var child in scene.Children)
            {
                var result = SelectNode(child, segments, 0);
                if (result != null)
                    return result;
            }

            throw new InvalidOperationException($"XPath '{xpath}' was not found.");
        }

        public static JsonDocument ProjectJson(JsonDocument document, TreeOptions options)
        {
            var xpaths = GetXpaths(options);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteProjectedNode(writer, document.RootElement, "", xpaths, options.Full);
            }
            return JsonDocument.Parse(stream.ToArray());
        }

        private static HashSet<string> GetXpaths(TreeOptions options)
        {
            return options.Xpaths
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static void PrintSceneNode(IHasTreeNodes parent, IRszSceneNode node, string currentPath, HashSet<string> xpaths, bool full)
        {
            if (node is RszFolder folder)
            {
                var folderName = folder.Name;
                var newPath = string.IsNullOrEmpty(currentPath) ? folderName : $"{currentPath}/{folderName}";

                var externalPath = GetFolderExternalPath(folder);
                var label = string.IsNullOrEmpty(externalPath)
                    ? $"[lightskyblue1]{Escape(folderName)}/[/]"
                    : $"[lightskyblue1]{Escape(folderName)}/[/] [dim](external: {Escape(externalPath)})[/]";
                var treeNode = parent.AddNode(label);

                foreach (var child in folder.Children)
                {
                    PrintSceneNode(treeNode, child, newPath, xpaths, full);
                }
            }
            else if (node is RszGameObject gameObject)
            {
                var goName = gameObject.Name;
                var goGuid = gameObject.Guid.ToString();
                var newPath = string.IsNullOrEmpty(currentPath) ? goName : $"{currentPath}/{goName}";

                var componentList = gameObject.Components
                    .Where(c => c.Type.Name != "via.Transform")
                    .Select(c => c.Type.Name)
                    .ToList();

                var componentSummary = GetComponentSummary(componentList);
                var label = string.IsNullOrEmpty(componentSummary)
                    ? $"[white]{Escape(goName)}[/] [green]{goGuid}[/]"
                    : $"[white]{Escape(goName)}[/] [green]{goGuid}[/] {componentSummary}";
                var treeNode = parent.AddNode(label);

                if (ShouldExpandComponents(xpaths, full, newPath, goName, goGuid))
                {
                    foreach (var component in gameObject.Components)
                    {
                        PrintComponentNode(treeNode, component);
                    }
                }

                foreach (var child in gameObject.Children)
                {
                    PrintSceneNode(treeNode, child, newPath, xpaths, full);
                }
            }
        }

        private static void PrintComponentNode(IHasTreeNodes parent, RszObjectNode component)
        {
            var compLabel = $"[orange1]{{{Escape(component.Type.Name)}}}[/]";
            var compNode = parent.AddNode(compLabel);
            PrintObjectFields(compNode, component);
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
                var label = $"[white]{Escape(fieldName)}[/]";
                var treeNode = SafeAddNode(parent, label);
                PrintObjectFields(treeNode, objectNode);
            }
            else if (node is RszArrayNode arrayNode)
            {
                var label = $"[white]{Escape(fieldName)}[/] [grey]{arrayNode.Length}[/]";
                var treeNode = SafeAddNode(parent, label);
                for (var i = 0; i < arrayNode.Length; i++)
                {
                    PrintFieldNode(treeNode, $"[{i}]", arrayNode[i]);
                }
            }
            else
            {
                var valueStr = FormatValue(node);
                var label = $"[white]{Escape(fieldName)}[/] = [green]{Escape(valueStr)}[/]";
                SafeAddNode(parent, label);
            }
        }

        private static bool MatchesFilter(string value, HashSet<string> xpaths)
        {
            if (xpaths.Count == 0)
                return false;

            foreach (var xpath in xpaths)
            {
                if (value.Equals(xpath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ShouldExpandComponents(HashSet<string> xpaths, bool full, string path, string name, string guid)
        {
            return full || MatchesFilter(path, xpaths) || MatchesFilter(name, xpaths) || MatchesFilter(guid, xpaths);
        }

        private static string GetComponentSummary(List<string> componentList)
        {
            if (componentList.Count == 0)
                return "";

            var limited = componentList.Take(3).Select(Escape).ToList();
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
            catch
            {
            }

            return null;
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
                    System.Numerics.Vector2 vec2 => $"<{vec2.X}, {vec2.Y}>",
                    System.Numerics.Vector3 vec3 => $"<{vec3.X}, {vec3.Y}, {vec3.Z}>",
                    System.Numerics.Vector4 vec4 => $"<{vec4.X}, {vec4.Y}, {vec4.Z}, {vec4.W}>",
                    System.Numerics.Quaternion q => $"<{q.X}, {q.Y}, {q.Z}, {q.W}>",
                    Guid g => g.ToString(),
                    bool b => b.ToString(),
                    _ => value?.ToString() ?? "null"
                };
            }

            return node.ToString() ?? "?";
        }

        private static string Escape(string text)
        {
            return Markup.Escape(text);
        }

        private static string NormalizePath(string path)
        {
            return path.TrimEnd('/');
        }

        private static void WriteProjectedNode(Utf8JsonWriter writer, JsonElement element, string currentPath, HashSet<string> xpaths, bool full)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                element.WriteTo(writer);
                return;
            }

            var type = element.TryGetProperty("@type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null;

            if (type == "via.GameObject")
            {
                WriteGameObject(writer, element, currentPath, xpaths, full);
                return;
            }

            if (type == "via.Folder")
            {
                WriteFolder(writer, element, currentPath, xpaths, full);
                return;
            }

            if (element.TryGetProperty("@children", out _))
            {
                WriteSceneRoot(writer, element, currentPath, xpaths, full);
                return;
            }

            WriteGenericObject(writer, element);
        }

        private static void WriteSceneRoot(Utf8JsonWriter writer, JsonElement element, string currentPath, HashSet<string> xpaths, bool full)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("@children"))
                {
                    WriteChildren(writer, property.Value, currentPath, xpaths, full);
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        private static void WriteFolder(Utf8JsonWriter writer, JsonElement element, string currentPath, HashSet<string> xpaths, bool full)
        {
            var name = element.TryGetProperty("Name", out var nameElement) ? nameElement.GetString() ?? "" : "";
            var nextPath = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath}/{name}";

            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("@children"))
                {
                    WriteChildren(writer, property.Value, nextPath, xpaths, full);
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        private static void WriteGameObject(Utf8JsonWriter writer, JsonElement element, string currentPath, HashSet<string> xpaths, bool full)
        {
            var name = element.TryGetProperty("Name", out var nameElement) ? nameElement.GetString() ?? "" : "";
            var guid = element.TryGetProperty("@guid", out var guidElement) ? guidElement.GetString() ?? "" : "";
            var nextPath = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath}/{name}";
            var expandComponents = ShouldExpandComponents(xpaths, full, nextPath, name, guid);

            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("@components"))
                {
                    WriteComponents(writer, property.Value, expandComponents);
                }
                else if (property.NameEquals("@children"))
                {
                    WriteChildren(writer, property.Value, nextPath, xpaths, full);
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        private static void WriteChildren(Utf8JsonWriter writer, JsonElement children, string currentPath, HashSet<string> xpaths, bool full)
        {
            writer.WriteStartArray();
            foreach (var child in children.EnumerateArray())
            {
                WriteProjectedNode(writer, child, currentPath, xpaths, full);
            }
            writer.WriteEndArray();
        }

        private static void WriteComponents(Utf8JsonWriter writer, JsonElement components, bool expandComponents)
        {
            writer.WriteStartArray();
            foreach (var component in components.EnumerateArray())
            {
                if (expandComponents)
                {
                    component.WriteTo(writer);
                    continue;
                }

                writer.WriteStartObject();
                if (component.TryGetProperty("@type", out var typeElement))
                {
                    writer.WritePropertyName("@type");
                    typeElement.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteGenericObject(Utf8JsonWriter writer, JsonElement element)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        private static IRszNode? SelectNode(IRszSceneNode node, string[] segments, int index)
        {
            if (!MatchesSegment(node, segments[index]))
                return null;

            if (index == segments.Length - 1)
                return (IRszNode)node;

            foreach (var child in node.Children)
            {
                var result = SelectNode(child, segments, index + 1);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static bool MatchesSegment(IRszSceneNode node, string segment)
        {
            return node switch
            {
                RszFolder folder => folder.Name.Equals(segment, StringComparison.OrdinalIgnoreCase),
                RszGameObject gameObject => gameObject.Name.Equals(segment, StringComparison.OrdinalIgnoreCase) ||
                                            gameObject.Guid.ToString().Equals(segment, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}
