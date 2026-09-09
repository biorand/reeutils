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
    /// Handler for Onimusha WotS point-graph files (.pog.12). The container is decoded into its node
    /// table + two RSZ streams; the placed-context objects (the main stream, i.e. each node's type id
    /// and world transform) are the primary editable content, and the graph-level container object
    /// (the secondary stream) is exposed alongside. The node table maps each graph node to a position
    /// in the main object list, so adding/removing a node means editing both the objects array and the
    /// node table (in the @meta-pog block).
    /// </summary>
    internal sealed class PointGraphFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new PogFile(Version, Data);
            var summary = CreateSummary("POG");
            summary["Version"] = file.Version;
            summary["RSZ version"] = file.RszVersion;
            summary["Nodes"] = file.NodeCount;
            summary["Instances"] = file.InstanceCount;
            summary["Graph hash"] = $"0x{file.GraphHash:X8}";
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            var file = new PogFile(Version, Data);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@meta-pog");
                writer.WriteStartObject();
                writer.WriteNumber("version", file.Version);
                writer.WriteString("hash", file.Hash.ToString());
                writer.WriteNumber("unknown", file.Unknown);
                writer.WriteNumber("graphHash", file.GraphHash);
                writer.WriteNumber("nodeCount", file.NodeCount);
                writer.WriteBoolean("hasMainRsz", file.HasMainRsz);
                writer.WritePropertyName("headerSlots");
                writer.WriteStartArray();
                for (var i = 0; i < 3 && i < file.HeaderSlots.Length; i++)
                {
                    writer.WriteNumberValue(file.HeaderSlots[i]);
                }
                writer.WriteEndArray();
                writer.WritePropertyName("nodeTable");
                writer.WriteStartArray();
                foreach (var entry in file.NodeEntries)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("objectIndex", entry.ObjectIndex);
                    writer.WriteNumber("reservedA", entry.ReservedA);
                    writer.WriteNumber("reservedB", entry.ReservedB);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WritePropertyName("nodeSection");
                writer.WriteBase64StringValue(file.NodeSectionBytes);
                writer.WritePropertyName("middle");
                writer.WriteBase64StringValue(file.MiddleBytes);
                writer.WriteEndObject();

                writer.WritePropertyName("graph");
                writer.WriteStartArray();
                foreach (var obj in file.ReadGraphObjects(Repository))
                {
                    writer.WriteRawValue(RszJsonSerializer.Serialize(obj, Repository, JsonSupport.CreateOptions()));
                }
                writer.WriteEndArray();

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
            var file = new PogFile(Version, Data);
            var results = new List<string>();
            foreach (var obj in file.ReadObjects(Repository))
            {
                SearchNode(obj, obj.Type.Name, pattern, results.Add);
            }
            return results;
        }

        public override byte[] Import(JsonDocument json)
        {
            var builder = new PogFile.Builder(Repository, Version);
            if (json.RootElement.TryGetProperty("@meta-pog", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("version", out var versionEl))
                    builder.Version = versionEl.GetInt32();
                if (meta.TryGetProperty("hash", out var hashEl) && hashEl.ValueKind == JsonValueKind.String)
                    builder.Hash = ulong.Parse(hashEl.GetString()!);
                if (meta.TryGetProperty("unknown", out var unknownEl))
                    builder.Unknown = unknownEl.GetUInt32();
                if (meta.TryGetProperty("graphHash", out var graphHashEl))
                    builder.GraphHash = graphHashEl.GetUInt32();
                if (meta.TryGetProperty("nodeCount", out var nodeCountEl))
                    builder.NodeCount = nodeCountEl.GetInt32();
                if (meta.TryGetProperty("hasMainRsz", out var hasMainEl))
                    builder.HasMainRsz = hasMainEl.GetBoolean();
                if (meta.TryGetProperty("headerSlots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Array)
                {
                    var slots = slotsEl.EnumerateArray().Select(e => e.GetInt64()).ToArray();
                    builder.HeaderSlots = [.. slots];
                }
                if (meta.TryGetProperty("nodeSection", out var nodeSectionEl) && nodeSectionEl.ValueKind == JsonValueKind.String)
                    builder.NodeSectionBytes = Convert.FromBase64String(nodeSectionEl.GetString()!);
                if (meta.TryGetProperty("middle", out var middleEl) && middleEl.ValueKind == JsonValueKind.String)
                    builder.MiddleBytes = Convert.FromBase64String(middleEl.GetString()!);
                if (meta.TryGetProperty("nodeTable", out var tableEl) && tableEl.ValueKind == JsonValueKind.Array)
                {
                    builder.NodeEntries = [.. tableEl.EnumerateArray().Select(e => new PogFile.PogNodeEntry(
                        e.GetProperty("objectIndex").GetUInt32(),
                        e.TryGetProperty("reservedA", out var ra) && ra.ValueKind != JsonValueKind.Null ? ra.GetUInt32() : 0u,
                        e.TryGetProperty("reservedB", out var rb) && rb.ValueKind != JsonValueKind.Null ? rb.GetUInt64() : 0ul))];
                }
            }

            builder.GraphObjects = ReadArray(json, "graph", Repository);
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