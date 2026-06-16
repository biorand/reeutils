using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Fsm;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class HfsmFileHandler(string path, byte[] data) : FileHandlerBase(path, data)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new HfsmFile(Data);
            var summary = CreateSummary("HFSM");
            summary["Version"] = file.Version;
            summary["Header size"] = file.HeaderSize;
            summary["Flags"] = $"0x{file.Flags:X2}";
            summary["State data entries"] = file.StateDataEntryCount;
            summary["States"] = file.States.Length;
            summary["Transition groups"] = file.TransitionGroups.Length;
            summary["Transition infos"] = file.TransitionInfos.Length;
            summary["Action references"] = file.ActionReferences.Length;
            summary["Strings"] = file.Strings.Length;
            summary["Extra strings"] = file.ExtraStrings.Length;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            if (options.ExpandNodes.Length > 0)
                throw new System.NotSupportedException("expand_nodes is not supported for .fsm files.");
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
