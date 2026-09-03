using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils
{
    internal static class JsonSupport
    {
        public static JsonSerializerOptions CreateOptions(bool camelCase = false)
        {
            return new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = camelCase ? JsonNamingPolicy.CamelCase : null,
                WriteIndented = true
            };
        }

        public static JsonDocument ToDocument<T>(T value, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.SerializeToDocument(value, options ?? CreateOptions());
        }

        public static string ToJsonString(JsonDocument document)
        {
            return JsonSerializer.Serialize(document.RootElement, CreateOptions());
        }

        public static JsonDocument ApplyTreeOptions(JsonDocument document, TreeOptions options)
        {
            if (options.Full || options.ExpandNodes.Length == 0)
                return JsonDocument.Parse(document.RootElement.GetRawText());

            var expandNodes = options.ExpandNodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteFilteredElement(writer, document.RootElement, "", expandNodes);
            }
            return JsonDocument.Parse(stream.ToArray());
        }

        private static void WriteFilteredElement(Utf8JsonWriter writer, JsonElement element, string currentPath, HashSet<string> expandNodes)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var type = TryGetString(element, "@type");
                    var name = TryGetString(element, "Name");
                    var guid = TryGetString(element, "@guid");
                    var propCount = CountObjectProperties(element);

                    bool expanded = propCount <= 3;

                    if (!expanded)
                    {
                        foreach (var node in expandNodes)
                        {
                            if (MatchesAny(name, node) || MatchesAny(guid, node) || MatchesAny(currentPath, node))
                            {
                                expanded = true;
                                break;
                            }
                        }
                    }

                    if (!expanded && type != null)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("@type", type);
                        writer.WriteEndObject();
                        return;
                    }

                    writer.WriteStartObject();
                    foreach (var prop in element.EnumerateObject())
                    {
                        var childPath = BuildPath(currentPath, prop.Name, name, prop.Value);
                        writer.WritePropertyName(prop.Name);
                        WriteFilteredElement(writer, prop.Value, childPath, expandNodes);
                    }
                    writer.WriteEndObject();
                    break;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    int i = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        WriteFilteredElement(writer, item, $"{currentPath}/{i}", expandNodes);
                        i++;
                    }
                    writer.WriteEndArray();
                    break;

                default:
                    element.WriteTo(writer);
                    break;
            }
        }

        private static string BuildPath(string currentPath, string propName, string? parentName, JsonElement propValue)
        {
            if (propName == "@type" || propName == "@guid")
                return parentName ?? currentPath;

            var basePath = string.IsNullOrEmpty(parentName) ? currentPath : parentName;
            return string.IsNullOrEmpty(basePath) ? propName : $"{basePath}/{propName}";
        }

        private static bool MatchesAny(string? value, string node)
        {
            if (value == null)
                return false;
            return value.Equals(node, StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith(node + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        private static int CountObjectProperties(JsonElement element)
        {
            int count = 0;
            foreach (var _ in element.EnumerateObject())
                count++;
            return count;
        }

        public static Tree CreateTree(JsonDocument document, string title)
        {
            var root = new Tree(Markup.Escape(string.IsNullOrEmpty(title) ? "root" : title));
            AddElement(root, document.RootElement);
            return root;
        }

        private static void AddElement(IHasTreeNodes parent, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (IsLeaf(property.Value))
                        {
                            parent.AddNode($"[white]{Escape(property.Name)}[/] = [green]{Escape(Format(property.Value))}[/]");
                        }
                        else
                        {
                            var child = parent.AddNode(GetContainerLabel(property.Name, property.Value));
                            AddElement(child, property.Value);
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var childElement in element.EnumerateArray())
                    {
                        if (IsLeaf(childElement))
                        {
                            parent.AddNode($"[white][{index}][/] = [green]{Escape(Format(childElement))}[/]");
                        }
                        else
                        {
                            var child = parent.AddNode(GetContainerLabel($"[{index}]", childElement));
                            AddElement(child, childElement);
                        }
                        index++;
                    }
                    break;

                default:
                    parent.AddNode($"[green]{Escape(Format(element))}[/]");
                    break;
            }
        }

        private static string GetContainerLabel(string label, JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                return $"[white]{Escape(label)}[/] [grey]{element.GetArrayLength()}[/]";
            }

            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("@type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String)
            {
                return $"[white]{Escape(label)}[/] [orange1]{{{Escape(typeElement.GetString()!)}}}[/]";
            }

            return $"[white]{Escape(label)}[/]";
        }

        private static bool IsLeaf(JsonElement element)
        {
            return element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.Array;
        }

        private static string Escape(string value) => Markup.Escape(value);

        private static string Format(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "null",
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => element.GetRawText()
            };
        }

    }
}
