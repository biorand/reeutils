using System;
using System.Buffers;
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
            var root = SelectElement(document.RootElement, options.Xpath);
            return CloneWithDepth(root, options.Depth);
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

        private static JsonElement SelectElement(JsonElement root, string xpath)
        {
            if (string.IsNullOrWhiteSpace(xpath))
                return root;

            var current = root;
            foreach (var token in Tokenize(xpath))
            {
                current = token.Kind switch
                {
                    JsonPathTokenKind.Property when current.ValueKind == JsonValueKind.Object && current.TryGetProperty(token.Value, out var property)
                        => property,
                    JsonPathTokenKind.Index when current.ValueKind == JsonValueKind.Array && int.TryParse(token.Value, out var index) && index >= 0 && index < current.GetArrayLength()
                        => current[index],
                    _ => throw new InvalidOperationException($"XPath '{xpath}' was not found.")
                };
            }

            return current;
        }

        private static IEnumerable<JsonPathToken> Tokenize(string xpath)
        {
            var current = "";
            for (var i = 0; i < xpath.Length; i++)
            {
                var ch = xpath[i];
                if (ch == '/' || ch == '.')
                {
                    if (current.Length > 0)
                    {
                        yield return new JsonPathToken(JsonPathTokenKind.Property, current);
                        current = "";
                    }
                    continue;
                }

                if (ch == '[')
                {
                    if (current.Length > 0)
                    {
                        yield return new JsonPathToken(JsonPathTokenKind.Property, current);
                        current = "";
                    }

                    var endBracket = xpath.IndexOf(']', i + 1);
                    if (endBracket == -1)
                        throw new InvalidOperationException($"XPath '{xpath}' is invalid.");

                    yield return new JsonPathToken(JsonPathTokenKind.Index, xpath.Substring(i + 1, endBracket - i - 1));
                    i = endBracket;
                    continue;
                }

                current += ch;
            }

            if (current.Length > 0)
                yield return new JsonPathToken(JsonPathTokenKind.Property, current);
        }

        private static JsonDocument CloneWithDepth(JsonElement root, int depth)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteWithDepth(writer, root, depth <= 0 ? -1 : depth);
            }
            return JsonDocument.Parse(stream.ToArray());
        }

        private static void WriteWithDepth(Utf8JsonWriter writer, JsonElement element, int remainingDepth)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    if (remainingDepth != 0)
                    {
                        var nextDepth = remainingDepth < 0 ? -1 : remainingDepth - 1;
                        foreach (var property in element.EnumerateObject())
                        {
                            writer.WritePropertyName(property.Name);
                            WriteWithDepth(writer, property.Value, nextDepth);
                        }
                    }
                    writer.WriteEndObject();
                    break;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    if (remainingDepth != 0)
                    {
                        var nextDepth = remainingDepth < 0 ? -1 : remainingDepth - 1;
                        foreach (var child in element.EnumerateArray())
                        {
                            WriteWithDepth(writer, child, nextDepth);
                        }
                    }
                    writer.WriteEndArray();
                    break;

                default:
                    element.WriteTo(writer);
                    break;
            }
        }

        private enum JsonPathTokenKind
        {
            Property,
            Index
        }

        private readonly record struct JsonPathToken(JsonPathTokenKind Kind, string Value);
    }
}
