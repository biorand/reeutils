using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class PrefabFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new PfbFile(Version, Data);
            var summary = CreateSummary("PFB");
            summary["Version"] = file.Version;
            summary["RSZ version"] = file.RszVersion;
            summary["Instances"] = file.InstanceCount;
            summary["Resources"] = file.Resources.Length;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            var file = new PfbFile(Version, Data);
            var scene = file.ReadScene(Repository);
            using var raw = SerializePrefab(scene, file.LooseObjects);
            var json = SceneTreeRenderer.ProjectJson(raw, options);
            return PrefabJsonMetadata.Apply(json, GetMetadata(file));
        }

        private Dictionary<string, byte[]> GetMetadata(PfbFile file)
        {
            var metadata = new Dictionary<string, byte[]>();

            // v16 engine-assigned property ids are not in RSZ dumps; carry the raw ref
            // table through the JSON hop so Import can replay it verbatim.
            if (file.Version < 17 && file.GameObjectRefCount > 0)
            {
                var refs = new byte[file.GameObjectRefCount * 16];
                file.ReadGameObjectRefData(refs);
                metadata["@gorefs"] = refs;
            }

            // The resource preload table is not derivable from the scene graph (entries
            // can exist without any RszResourceNode, and tables can be legitimately
            // empty); carry it verbatim for import — including the empty case.
            var resources = file.Resources;
            {
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(resources.Length);
                    // NUL-terminated UTF16 strings; do NOT use Write(string), which
                    // prepends a 7-bit length prefix.
                    foreach (var resource in resources)
                    {
                        foreach (var ch in resource)
                        {
                            writer.Write(ch);
                        }
                        writer.Write('\0');
                    }
                }
                metadata["resources"] = stream.ToArray();
            }
            return metadata;
        }

        public override IEnumerable<string> Search(Regex pattern)
        {
            var scene = new PfbFile(Version, Data).ReadScene(Repository);
            var results = new List<string>();
            foreach (var child in scene.Children)
            {
                SearchNode(child, "", pattern, results.Add);
            }
            return results;
        }

        public override Tree GetTree(TreeOptions options)
        {
            var scene = new PfbFile(Version, Data).ReadScene(Repository);
            return SceneTreeRenderer.CreateTree(scene, options);
        }

        public override byte[] Import(JsonDocument json)
        {
            var builder = CreateBuilder();
            var metadata = PrefabJsonMetadata.Extract(json);
            builder.Scene = RszJsonSerializer.DeserializePrefabScene(json, Repository, out var looseObjects);
            foreach (var loose in looseObjects)
            {
                builder.LooseObjects.Add(loose);
            }
            if (metadata.TryGetValue("gorefs", out var gorefs))
            {
                builder.PreservedGameObjectRefData = gorefs;
            }
            if (metadata.ContainsKey("resources"))
            {
                // The JSON carried the preload table (possibly empty); the vanilla table is
                // authoritative and must not be re-harvested from the scene graph.
                if (!metadata.TryGetValue("resources", out var rawResources))
                    return builder.Build().Data.ToArray();

                builder.Resources.Clear();
                using var stream = new MemoryStream(rawResources);
                using (var reader = new BinaryReader(stream))
                {
                    var count = reader.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        var chars = new List<char>();
                        while (true)
                        {
                            var ch = reader.ReadChar();
                            if (ch == '\0')
                                break;
                            chars.Add(ch);
                        }
                        builder.Resources.Add(new string(chars.ToArray()));
                    }
                }
            }
            else
            {
                // Hand-authored JSON without metadata: harvest resources from the scene.
                builder.RebuildResources();
            }
            return builder.Build().Data.ToArray();
        }

        private JsonDocument SerializePrefab(RszScene scene, ImmutableArray<RszObjectNode> looseObjects)
        {
            if (looseObjects.IsEmpty)
            {
                // No standalone objects: keep the plain scene document shape.
                return SerializeNode(scene, TreeOptions.Root);
            }
            var json = RszJsonSerializer.SerializePrefabDocument(scene, looseObjects, Repository, JsonSupport.CreateOptions());
            return JsonDocument.Parse(json);
        }

        private PfbFile.Builder CreateBuilder()
        {
            var template = EmbeddedData.GetFile($"empty.pfb.{Version}");
            if (template != null)
                return new PfbFile(Version, template).ToBuilder(Repository);
            if (TryGetRszVersion(out var rszVersion))
                return new PfbFile.Builder(Repository, Version, rszVersion);
            throw new NotSupportedException($"No embedded template exists for .pfb.{Version}.");
        }

        private bool TryGetRszVersion(out int rszVersion)
        {
            // .16 prefabs are RE2/RE3 (non-RT); their inner RSZ streams are always version 8.
            rszVersion = Version == 16 ? 8 : 0;
            return rszVersion != 0;
        }
    }
}
