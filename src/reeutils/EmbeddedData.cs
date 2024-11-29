using System.IO;
using System.IO.Compression;
using System.Reflection;
using REE;

namespace IntelOrca.Biohazard.REEUtils
{
    internal class EmbeddedData
    {
        public static PakList? GetPakList(string name)
        {
            var assembly = Assembly.GetExecutingAssembly()!;
            var resourceName = $"IntelOrca.Biohazard.REEUtils.data.paklist.{name}.txt.gz";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            var result = reader.ReadToEnd();
            return new PakList(result);
        }
    }
}
