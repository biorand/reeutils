using System;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class PrefabFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override JsonDocument GetJson(TreeOptions options)
        {
            var scene = new PfbFile(Version, Data).ReadScene(Repository);
            return SerializeNode(scene, options);
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
