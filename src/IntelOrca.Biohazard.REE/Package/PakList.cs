using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.REE.Cryptography;

namespace IntelOrca.Biohazard.REE.Package
{
    public sealed class PakList
    {
        private readonly Dictionary<ulong, string> _map = [];

        public ImmutableArray<string> Entries { get; }

        public static PakList FromFile(string path) => new PakList(File.ReadAllText(path));

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
                    _map[GetHash(path)] = path;
                }
            }
            Entries = [.. _map.Values.OrderBy(x => x)];
        }

        public string? GetPath(ulong hash)
        {
            _map.TryGetValue(hash, out var path);
            return path;
        }

        private static ulong GetHash(string path)
        {
            var dwHashLower = (ulong)MurMur3.HashData(path.ToLower());
            var dwHashUpper = (ulong)MurMur3.HashData(path.ToUpper());
            return dwHashLower | (dwHashUpper << 32);
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
