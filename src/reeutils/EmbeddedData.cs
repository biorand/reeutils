using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using IntelOrca.Biohazard.REE.Package;

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

        public static byte[]? GetCompressedFile(string name)
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
