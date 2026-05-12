using System.Text.Json;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal interface IFileHandler
    {
        bool RequiresTypeRepository { get; }
        JsonDocument GetJson(TreeOptions options);
        Tree GetTree(TreeOptions options);
        byte[] Import(JsonDocument json);
    }
}
