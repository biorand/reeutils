using System.IO;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Fsm;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class HfsmFileHandler(string path, byte[] data) : FileHandlerBase(path, data)
    {
        public override JsonDocument GetJson(TreeOptions options)
        {
            var graph = HfsmGraphDocument.FromFile(new HfsmFile(Data));
            using var document = JsonSupport.ToDocument(graph, JsonSupport.CreateOptions());
            return JsonSupport.ApplyTreeOptions(document, options);
        }

        public override byte[] Import(JsonDocument json)
        {
            var graph = JsonSerializer.Deserialize<HfsmGraphDocument>(json.RootElement.GetRawText(), JsonSupport.CreateOptions())
                ?? throw new InvalidDataException("Failed to deserialize HFSM graph.");
            return graph.Build().Data.ToArray();
        }
    }
}
