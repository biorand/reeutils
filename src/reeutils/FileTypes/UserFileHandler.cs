using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class UserFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new UserFile(Data);
            var summary = CreateSummary("USER");
            summary["RSZ version"] = file.RszVersion;
            summary["Instance count"] = file.InstanceCount;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            var file = new UserFile(Data);
            var objects = file.GetObjects(Repository);
            using var raw = CreateDocument(objects);
            var json = JsonSupport.ApplyTreeOptions(raw, options);

            // Resource-bearing .user files (wrapper resource table / userdata before the RSZ stream)
            // can't be reconstructed from the object list alone; carry the wrapper prefix verbatim so
            // import doesn't drop it. Plain files (RSZ directly after the header) keep the flat shape.
            if (file.RszDataOffset > 48)
            {
                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("@meta-user");
                    writer.WriteStartObject();
                    writer.WritePropertyName("prefix");
                    writer.WriteBase64StringValue(file.Prefix);
                    writer.WriteEndObject();
                    foreach (var property in json.RootElement.EnumerateObject())
                    {
                        property.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                ms.Position = 0;
                json.Dispose();
                return JsonDocument.Parse(ms);
            }
            return json;
        }

        public override IEnumerable<string> Search(Regex pattern)
        {
            var objects = new UserFile(Data).GetObjects(Repository);
            var results = new List<string>();
            foreach (var obj in objects)
            {
                SearchNode(obj, obj.Type.Name, pattern, results.Add);
            }
            return results;
        }

        public override byte[] Import(JsonDocument json)
        {
            var template = EmbeddedData.GetFile($"empty.user.{Version}") ?? throw new NotSupportedException($"No embedded template exists for .user.{Version}.");
            var builder = new UserFile(template).ToBuilder(Repository);

            // Resource-bearing exports carry the wrapper prefix; restore it so the resource table /
            // wrapper userdata survives the JSON hop. Flat exports (plain header) use the template.
            if (json.RootElement.TryGetProperty("@meta-user", out var meta) &&
                meta.ValueKind == JsonValueKind.Object &&
                meta.TryGetProperty("prefix", out var prefixEl) && prefixEl.ValueKind == JsonValueKind.String)
            {
                builder.PreservedPrefix = Convert.FromBase64String(prefixEl.GetString()!);
                var objectsElement = json.RootElement.TryGetProperty("objects", out var objEl) ? objEl : json.RootElement;
                builder.Objects = objectsElement.ValueKind == JsonValueKind.Array
                    ? [.. objectsElement.EnumerateArray().Select(x => (RszObjectNode)RszJsonSerializer.Deserialize(JsonDocument.Parse(x.GetRawText()), Repository))]
                    : [(RszObjectNode)RszJsonSerializer.Deserialize(JsonDocument.Parse(objectsElement.GetRawText()), Repository)];
            }
            else
            {
                builder.Objects = json.RootElement.ValueKind == JsonValueKind.Array
                    ? [.. json.RootElement.EnumerateArray().Select(x => (RszObjectNode)RszJsonSerializer.Deserialize(JsonDocument.Parse(x.GetRawText()), Repository))]
                    : [(RszObjectNode)RszJsonSerializer.Deserialize(json, Repository)];
            }
            return builder.Build().Data.ToArray();
        }

        private static JsonDocument CreateDocument(ImmutableArray<RszObjectNode> objects)
        {
            if (objects.Length == 1)
                return JsonDocument.Parse(RszJsonSerializer.Serialize(objects[0], JsonSupport.CreateOptions()));

            var elements = objects
                .Select(x => JsonDocument.Parse(RszJsonSerializer.Serialize(x, JsonSupport.CreateOptions())).RootElement.Clone())
                .ToArray();
            return JsonSupport.ToDocument(elements, JsonSupport.CreateOptions());
        }
    }
}
