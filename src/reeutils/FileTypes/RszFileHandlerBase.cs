using System;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal abstract class RszFileHandlerBase(string path, byte[] data, int version, RszTypeRepository? repository) : FileHandlerBase(path, data)
    {
        protected int Version { get; } = version;
        protected RszTypeRepository Repository => repository ?? throw new InvalidOperationException("Game not specified. Use -g <game>.");

        public override bool RequiresTypeRepository => true;

        protected static JsonDocument SerializeNode(IRszNode node, TreeOptions options)
        {
            using var raw = JsonDocument.Parse(RszJsonSerializer.Serialize(node, JsonSupport.CreateOptions()));
            return JsonSupport.ApplyTreeOptions(raw, options);
        }
    }
}
