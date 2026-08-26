using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class SceneFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new ScnFile(Version, Data);
            var summary = CreateSummary("SCN");
            summary["Version"] = file.Version;
            summary["RSZ version"] = file.RszVersion;
            summary["Instances"] = file.InstanceCount;
            summary["Prefabs"] = file.Prefabs.Length;
            summary["Resources"] = file.Resources.Length;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            var file = new ScnFile(Version, Data);
            var scene = file.ReadScene(Repository);
            using var raw = SerializeNode(scene, TreeOptions.Root);
            using var json = SceneTreeRenderer.ProjectJson(raw, options);

            // Prefab/resource references are not necessarily part of the scene graph in
            // v19 scenes (preload entries are referenced only by string fields); carry
            // them as metadata so import can restore them. @resources is emitted even
            // when empty: the absence of a preload table (ResourceCount=0) is itself
            // information that would otherwise be re-harvested from the scene graph.
            var prefabs = file.Prefabs;
            var resources = file.Resources;
            var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                if (prefabs.Length > 0)
                {
                    writer.WritePropertyName("@prefabs");
                    writer.WriteStartArray();
                    foreach (var prefab in prefabs)
                    {
                        writer.WriteStringValue(prefab);
                    }
                    writer.WriteEndArray();
                }
                writer.WritePropertyName("@resources");
                writer.WriteStartArray();
                foreach (var resource in resources)
                {
                    writer.WriteStringValue(resource);
                }
                writer.WriteEndArray();
                foreach (var property in json.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            ms.Position = 0;
            return JsonDocument.Parse(ms);
        }

        public override IEnumerable<string> Search(Regex pattern)
        {
            var scene = new ScnFile(Version, Data).ReadScene(Repository);
            var results = new List<string>();
            foreach (var child in scene.Children)
            {
                SearchNode(child, "", pattern, results.Add);
            }
            return results;
        }

        public override Tree GetTree(TreeOptions options)
        {
            var scene = new ScnFile(Version, Data).ReadScene(Repository);
            return SceneTreeRenderer.CreateTree(scene, options);
        }

        public override byte[] Import(JsonDocument json)
        {
            var builder = CreateBuilder();
            builder.Scene = (RszScene)RszJsonSerializer.Deserialize(json, Repository);
            var hasResourceList = json.RootElement.TryGetProperty("@resources", out var resourcesElement) &&
                resourcesElement.ValueKind == JsonValueKind.Array;
            if (hasResourceList)
            {
                // The game's resource preload table is not simply the set of RszResourceNodes
                // in the scene (some nodes are excluded, some entries have no node), so
                // restore it verbatim rather than rebuilding.
                foreach (var resource in resourcesElement.EnumerateArray())
                {
                    var path = resource.GetString();
                    if (!string.IsNullOrEmpty(path))
                        builder.Resources.Add(path);
                }
            }
            else
            {
                // No resource list in the JSON (hand-authored file): rebuild from the scene.
                builder.RebuildResources();
            }
            builder.Prefabs.Clear();
            if (json.RootElement.TryGetProperty("@prefabs", out var prefabsElement) &&
                prefabsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var prefab in prefabsElement.EnumerateArray())
                {
                    var path = prefab.GetString();
                    if (!string.IsNullOrEmpty(path))
                        builder.Prefabs.Add(path);
                }
            }
            else
            {
                builder.Scene.VisitGameObjects(go =>
                {
                    if (!string.IsNullOrEmpty(go.Prefab) && !builder.Prefabs.Any(p => string.Equals(p, go.Prefab, StringComparison.OrdinalIgnoreCase)))
                        builder.Prefabs.Add(go.Prefab!);
                });
            }
            return builder.Build().Data.ToArray();
        }

        private ScnFile.Builder CreateBuilder()
        {
            var template = EmbeddedData.GetFile($"empty.scn.{Version}");
            if (template != null)
                return new ScnFile(Version, template).ToBuilder(Repository);
            if (TryGetRszVersion(out var rszVersion))
                return new ScnFile.Builder(Repository, Version, rszVersion);
            throw new NotSupportedException($"No embedded template exists for .scn.{Version}.");
        }

        private bool TryGetRszVersion(out int rszVersion)
        {
            // .19 scenes are RE2/RE3 (non-RT); their inner RSZ streams are always version 8.
            rszVersion = Version == 19 ? 8 : 0;
            return rszVersion != 0;
        }
    }
}
