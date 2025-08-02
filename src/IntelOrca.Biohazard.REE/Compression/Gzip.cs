using System.IO;
using System.IO.Compression;

namespace IntelOrca.Biohazard.REE.Compression
{
    public static class Gzip
    {
        public static byte[] CompressData(byte[] buffer)
        {
            using var outputStream = new MemoryStream();
            using var inputStream = new MemoryStream(buffer);
            using var deflateStream = new GZipStream(inputStream, CompressionMode.Compress, false);
            deflateStream.CopyTo(outputStream);
            return outputStream.ToArray();

        }

        public static byte[] DecompressData(byte[] buffer)
        {
            using var outputStream = new MemoryStream();
            using var inputStream = new MemoryStream(buffer);
            using var deflateStream = new GZipStream(inputStream, CompressionMode.Decompress, false);
            deflateStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}
