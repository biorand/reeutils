using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    /// <summary>
    /// Handler for Onimusha WotS collider-set files (.cset.6). The native header and collider geometry
    /// are not RSZ-derivable, so they are carried verbatim (base64) in @meta-cset; the per-zone
    /// parameter objects (the RSZ stream) are the editable content.
    /// </summary>
    internal sealed class ColliderSetFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new CsetFile(Version, Data, Repository);
            var summary = CreateSummary("CSET");
            summary["Version"] = file.Version;
            summary["RSZ version"] = file.RszVersion;
            summary["Colliders"] = file.ReadObjects(Repository).Length;
            summary["Instances"] = file.InstanceCount;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            var file = new CsetFile(Version, Data, Repository);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@meta-cset");
                writer.WriteStartObject();
                writer.WriteNumber("version", file.Version);
                writer.WriteNumber("shapeListCount", file.ShapeListCount);
                writer.WriteNumber("shapeCount", file.ShapeCount);
                writer.WriteNumber("fileLength", Data.Length);
                writer.WriteNumber("secondaryOffset", file.SecondaryRszOffsetInTail);
                writer.WritePropertyName("boundarySlots");
                writer.WriteStartObject();
                foreach (var (offset, value) in file.BoundarySlots)
                {
                    writer.WriteNumber(offset.ToString(), value);
                }
                writer.WriteEndObject();
                writer.WritePropertyName("prefix");
                writer.WriteBase64StringValue(file.Prefix);
                writer.WritePropertyName("tail");
                writer.WriteBase64StringValue(file.Tail);
                writer.WriteEndObject();

                writer.WritePropertyName("objects");
                writer.WriteStartArray();
                foreach (var obj in file.ReadObjects(Repository))
                {
                    writer.WriteRawValue(RszJsonSerializer.Serialize(obj, Repository, JsonSupport.CreateOptions()));
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            ms.Position = 0;
            var raw = JsonDocument.Parse(ms);
            return JsonSupport.ApplyTreeOptions(raw, options);
        }

        public override IEnumerable<string> Search(Regex pattern)
        {
            var file = new CsetFile(Version, Data, Repository);
            var results = new List<string>();
            foreach (var obj in file.ReadObjects(Repository))
            {
                SearchNode(obj, obj.Type.Name, pattern, results.Add);
            }
            return results;
        }

        public override byte[] Import(JsonDocument json)
        {
            var builder = new CsetFile.Builder(Repository, Version);

            if (json.RootElement.TryGetProperty("@meta-cset", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("version", out var versionEl))
                    builder.Version = versionEl.GetInt32();
                if (meta.TryGetProperty("prefix", out var prefixEl) && prefixEl.ValueKind == JsonValueKind.String)
                    builder.Prefix = Convert.FromBase64String(prefixEl.GetString()!);
                if (meta.TryGetProperty("tail", out var tailEl) && tailEl.ValueKind == JsonValueKind.String)
                    builder.Tail = Convert.FromBase64String(tailEl.GetString()!);
                if (meta.TryGetProperty("fileLength", out var fileLengthEl))
                    builder.OldFileLength = fileLengthEl.GetInt32();
                if (meta.TryGetProperty("secondaryOffset", out var secondaryEl))
                    builder.SecondaryRszOffsetInTail = secondaryEl.GetInt32();
                if (meta.TryGetProperty("boundarySlots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Object)
                {
                    var slots = new Dictionary<int, long>();
                    foreach (var slot in slotsEl.EnumerateObject())
                    {
                        slots[int.Parse(slot.Name)] = slot.Value.GetInt64();
                    }
                    builder.BoundarySlots = slots;
                }
            }

            builder.Objects = ReadArray(json, "objects", Repository);
            return builder.Build().Data.ToArray();
        }

        private static ImmutableArray<RszObjectNode> ReadArray(JsonDocument json, string name, RszTypeRepository repository)
        {
            if (!json.RootElement.TryGetProperty(name, out var arrayEl) || arrayEl.ValueKind != JsonValueKind.Array)
                return [];
            return [.. arrayEl.EnumerateArray().Select(x => (RszObjectNode)RszJsonSerializer.Deserialize(JsonDocument.Parse(x.GetRawText()), repository))];
        }
    }
}