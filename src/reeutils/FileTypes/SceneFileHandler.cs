using System;
using System.Collections.Generic;
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
            var scene = new ScnFile(Version, Data).ReadScene(Repository);
            using var raw = SerializeNode(scene, TreeOptions.Root);
            return SceneTreeRenderer.ProjectJson(raw, options);
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
            var template = EmbeddedData.GetFile($"empty.scn.{Version}") ?? throw new NotSupportedException($"No embedded template exists for .scn.{Version}.");
            var builder = new ScnFile(Version, template).ToBuilder(Repository);
            builder.Scene = (RszScene)RszJsonSerializer.Deserialize(json, Repository);
            return builder.Build().Data.ToArray();
        }
    }
}
