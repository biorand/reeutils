using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class UserFileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override JsonDocument GetJson(TreeOptions options)
        {
            var objects = new UserFile(Data).GetObjects(Repository);
            using var raw = CreateDocument(objects);
            return JsonSupport.ApplyTreeOptions(raw, options);
        }

        public override byte[] Import(JsonDocument json)
        {
            var template = EmbeddedData.GetFile($"empty.user.{Version}") ?? throw new NotSupportedException($"No embedded template exists for .user.{Version}.");
            var builder = new UserFile(template).ToBuilder(Repository);
            builder.Objects = json.RootElement.ValueKind == JsonValueKind.Array
                ? [.. json.RootElement.EnumerateArray().Select(x => (RszObjectNode)RszJsonSerializer.Deserialize(JsonDocument.Parse(x.GetRawText()), Repository))]
                : [(RszObjectNode)RszJsonSerializer.Deserialize(json, Repository)];
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
