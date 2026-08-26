using System;
using System.Collections.Generic;
using System.Text.Json;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    /// <summary>
    /// Carries binary side-data (e.g. the raw v16 game object ref table) through the JSON hop
    /// as top-level base64 properties. Projection preserves unknown root properties, and
    /// <see cref="Extract"/> removes them before scene deserialization so handlers never see them.
    /// </summary>
    internal static class PrefabJsonMetadata
    {
        public static Dictionary<string, byte[]> Extract(JsonDocument json)
        {
            var result = new Dictionary<string, byte[]>();
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in root.EnumerateObject())
            {
                if (!property.Name.StartsWith("@meta-", StringComparison.Ordinal))
                    continue;
                if (property.Value.ValueKind == JsonValueKind.String &&
                    property.Value.TryGetBytesFromBase64(out var bytes))
                {
                    var name = property.Name.StartsWith("@meta-", StringComparison.Ordinal)
                        ? property.Name["@meta-".Length..]
                        : property.Name;
                    result[name] = bytes;
                }
            }
            return result;
        }

        public static JsonDocument Apply(JsonDocument json, Dictionary<string, byte[]> metadata)
        {
            if (metadata.Count == 0)
                return json;

            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return json;

            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var property in root.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                foreach (var (key, value) in metadata)
                {
                    writer.WriteBase64String(MetadataKey(key), value);
                }
                writer.WriteEndObject();
            }
            return JsonDocument.Parse(stream.ToArray());
        }

        private static string MetadataKey(string name) =>
            $"@meta-{name.TrimStart('@')}";
    }
}
