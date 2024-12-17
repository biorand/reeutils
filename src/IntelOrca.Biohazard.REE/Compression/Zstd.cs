using System.IO;
using ZstdSharp;

namespace IntelOrca.Biohazard.REE.Compression
{
    internal static class Zstd
    {
        public static byte[] CompressData(byte[] buffer)
        {
            using var inputStream = new MemoryStream(buffer);
            using var outputStream = new MemoryStream();
            using var zstdStream = new CompressionStream(outputStream);
            inputStream.CopyTo(zstdStream);
            zstdStream.Close();
            return outputStream.ToArray();
        }

        public static byte[] DecompressData(byte[] buffer)
        {
            using var inputStream = new MemoryStream(buffer);
            using var zstdStream = new DecompressionStream(inputStream);
            using var outputStream = new MemoryStream();
            zstdStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}
