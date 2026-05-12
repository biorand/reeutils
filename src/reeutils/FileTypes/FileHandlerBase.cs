using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal abstract class FileHandlerBase(string path, byte[] data) : IFileHandler
    {
        protected string Path { get; } = path;
        protected byte[] Data { get; } = data;

        public virtual bool RequiresTypeRepository => false;

        public abstract JsonDocument GetJson(TreeOptions options);
        public virtual IEnumerable<string> Search(Regex pattern)
        {
            using var json = GetJson(TreeOptions.Root);
            var text = JsonSupport.ToJsonString(json);
            foreach (Match match in pattern.Matches(text))
            {
                yield return match.Value;
            }
        }
        public abstract byte[] Import(JsonDocument json);

        public virtual Tree GetTree(TreeOptions options)
        {
            using var json = GetJson(options);
            return JsonSupport.CreateTree(json, System.IO.Path.GetFileName(Path));
        }
    }
}
