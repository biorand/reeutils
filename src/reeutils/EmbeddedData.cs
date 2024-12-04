using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using REE;
using RszTool;

namespace IntelOrca.Biohazard.REEUtils
{
    internal class EmbeddedData
    {
        public static PakList? GetPakList(string name)
        {
            var data = GetCompressedFile($"paklist.{name}.txt");
            if (data == null)
                return null;

            var content = Encoding.UTF8.GetString(data);
            return new PakList(content);
        }

        public static RszFileOption? CreateRszFileOption(string name)
        {
            if (!Enum.TryParse<GameName>(name, out var gameName))
                return null;

            var dataFile = Path.Combine($"rsz{gameName}.json");
            var enumFile = Path.Combine($"Enums/{gameName}_enum.json");
            var pathFile = Path.Combine($"RszPatch/rsz{gameName}_patch.json");

            var tempPath = Path.Combine(Path.GetTempPath(), "re4rr");
            Directory.CreateDirectory(tempPath);
            ExportFile("rszre4.json", Path.Combine(tempPath, dataFile));
            ExportFile("re4_enum.json", Path.Combine(tempPath, "Data", enumFile));
            ExportFile("rszre4_patch.json", Path.Combine(tempPath, "Data", pathFile));
            var cwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempPath);
                return new RszFileOption(gameName);
            }
            finally
            {
                Directory.SetCurrentDirectory(cwd);
            }
        }

        private static void ExportFile(string name, string path)
        {
            var data = GetCompressedFile(name);
            if (data == null)
                throw new Exception("Resource not found.");

            File.WriteAllBytes(path, data);
        }

        private static byte[]? GetCompressedFile(string name)
        {
            var assembly = Assembly.GetExecutingAssembly()!;
            var resourceName = $"IntelOrca.Biohazard.REEUtils.data.{name}.gz";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gzipStream.CopyTo(ms);
            return ms.ToArray();
        }

        public static byte[]? GetFile(string name)
        {
            var assembly = Assembly.GetExecutingAssembly()!;
            var resourceName = $"IntelOrca.Biohazard.REEUtils.data.{name}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
