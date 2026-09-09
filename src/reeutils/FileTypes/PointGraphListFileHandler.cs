using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    /// <summary>
    /// Handler for Onimusha WotS point-graph list files (.poglst.0). The container is a simple index
    /// of the <c>.pog</c> point-graph paths a <c>ContextLayouter</c> / <c>RandomSetPointFinder</c>
    /// loads. Editing the file array is how a RandomSet variant list's membership changes.
    /// </summary>
    internal sealed class PointGraphListFileHandler(string path, byte[] data, int version)
        : FileHandlerBase(path, data)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new PogListFile(version, Data);
            var summary = CreateSummary("POGLST");
            summary["Version"] = file.Version;
            summary["Point graphs"] = file.PogFiles.Length;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            var file = new PogListFile(version, Data);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", file.Version);
                writer.WriteString("hash", $"0x{file.Hash:X8}");
                writer.WritePropertyName("files");
                writer.WriteStartArray();
                foreach (var pogFile in file.PogFiles)
                {
                    writer.WriteStringValue(pogFile);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            ms.Position = 0;
            return JsonSupport.ApplyTreeOptions(JsonDocument.Parse(ms), options);
        }

        public override IEnumerable<string> Search(Regex pattern)
        {
            var file = new PogListFile(version, Data);
            return file.PogFiles.Where(p => pattern.IsMatch(p));
        }

        public override byte[] Import(JsonDocument json)
        {
            var builder = new PogListFile.Builder();
            if (json.RootElement.TryGetProperty("version", out var versionEl))
                builder.Version = versionEl.GetInt32();
            if (json.RootElement.TryGetProperty("hash", out var hashEl) && hashEl.ValueKind == JsonValueKind.String)
                builder.Hash = Convert.ToUInt32(hashEl.GetString()!.Replace("0x", ""), 16);
            builder.PogFiles.Clear();
            if (json.RootElement.TryGetProperty("files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileEl in filesEl.EnumerateArray())
                {
                    var value = fileEl.GetString();
                    if (!string.IsNullOrEmpty(value))
                        builder.PogFiles.Add(value);
                }
            }
            return builder.Build().Data.ToArray();
        }
    }
}