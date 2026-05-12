using System;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class SceneFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override JsonDocument GetJson(TreeOptions options)
        {
            var scene = new ScnFile(Version, Data).ReadScene(Repository);
            return SerializeNode(scene, options);
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
