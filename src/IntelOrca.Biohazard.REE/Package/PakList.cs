using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using IntelOrca.Biohazard.REE.Compression;

namespace IntelOrca.Biohazard.REE.Package
{
    public sealed class PakList
    {
        private readonly Dictionary<ulong, string> _map = [];

        public ImmutableArray<string> Entries { get; }

        public static PakList FromFile(string path)
        {
            var data = File.ReadAllBytes(path);
            if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                data = Gzip.DecompressData(data);
            }
            var sr = new StreamReader(new MemoryStream(data), Encoding.UTF8);
            var s = sr.ReadToEnd();
            return new PakList(s);
        }

        public PakList(string contents)
            : this(contents.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
        }

        public PakList(IEnumerable<string> files)
        {
            foreach (var line in files)
            {
                var path = line.Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    _map[PakFile.GetNormalizedPathHash(path)] = path;
                }
            }
            Entries = [.. _map.Values.OrderBy(x => x)];
        }

        public string? GetPath(ulong hash)
        {
            _map.TryGetValue(hash, out var path);
            return path;
        }

        /// <summary>
        /// Returns a new <see cref="PakList"/> containing only the entries found in the
        /// given pak file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public PakList Strip(PakFile file)
        {
            var fileNames = new List<string>();
            for (var i = 0; i < file.EntryCount; i++)
            {
                var fileName = file.GetEntryName(i, this);
                if (fileName != null)
                {
                    fileNames.Add(fileName);
                }
            }
            return new PakList(fileNames);
        }

        public string ToFileContents() => string.Join("\n", Entries) + "\n";
        public void WriteToFile(string path) => File.WriteAllText(path, ToFileContents());
    }
}
