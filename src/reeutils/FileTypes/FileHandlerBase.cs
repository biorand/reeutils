using System.IO;
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
        public abstract byte[] Import(JsonDocument json);

        public virtual Tree GetTree(TreeOptions options)
        {
            using var json = GetJson(options);
            var title = string.IsNullOrWhiteSpace(options.Xpath) ? System.IO.Path.GetFileName(Path) : options.Xpath;
            return JsonSupport.CreateTree(json, title);
        }
    }
}
