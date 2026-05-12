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

        public static JsonDocument ApplyTreeOptions(JsonDocument document, TreeOptions _)
        {
            return JsonDocument.Parse(document.RootElement.GetRawText());
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
