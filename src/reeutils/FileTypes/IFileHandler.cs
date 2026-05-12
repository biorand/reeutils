using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal interface IFileHandler
    {
        bool RequiresTypeRepository { get; }
        JsonDocument GetJson(TreeOptions options);
        Tree GetTree(TreeOptions options);
        IEnumerable<string> Search(Regex pattern);
        byte[] Import(JsonDocument json);
    }
}
