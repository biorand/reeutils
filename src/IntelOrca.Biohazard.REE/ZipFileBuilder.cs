using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace IntelOrca.Biohazard.REE
{
    public class ZipFileBuilder
    {
        private Dictionary<string, byte[]> _entries = new();

        public ZipFileBuilder AddEntry(string path, byte[] data)
        {
            _entries.Add(path, data);
            return this;
        }

        public byte[] Build()
        {
            using var ms = new MemoryStream();
            using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in _entries)
                {
                    var zipEntry = zipArchive.CreateEntry(entry.Key);
                    using var zipEntryStream = zipEntry.Open();
                    zipEntryStream.Write(entry.Value);
                }
            }
            return ms.ToArray();
        }
    }
}
