using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class PrefabFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override JsonDocument GetJson(TreeOptions options)
        {
            var scene = new PfbFile(Version, Data).ReadScene(Repository);
            using var raw = SerializeNode(scene, new TreeOptions
            {
                Depth = options.Depth
            });

            return options.CompactComponents
                ? SceneTreeRenderer.ProjectJson(raw, options)
                : JsonSupport.ApplyTreeOptions(raw, options);
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
            var template = EmbeddedData.GetFile($"empty.pfb.{Version}") ?? throw new NotSupportedException($"No embedded template exists for .pfb.{Version}.");
            var builder = new PfbFile(Version, template).ToBuilder(Repository);
            builder.Scene = (RszScene)RszJsonSerializer.Deserialize(json, Repository);
            return builder.Build().Data.ToArray();
        }
    }
}
