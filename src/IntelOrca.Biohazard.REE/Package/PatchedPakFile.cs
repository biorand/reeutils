using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard.REE.Package
{
    public sealed class PatchedPakFile : IPakFile, IDisposable
    {
        private readonly PakFileCollection _collection;

        public PatchedPakFile(string basePakPath)
        {
            if (!File.Exists(basePakPath))
                throw new FileNotFoundException($"{basePakPath} does not exist.", basePakPath);

            var fileOrder = new List<string>() { basePakPath };
            var basePakFileName = Path.GetFileName(basePakPath);
            var directory = Path.GetDirectoryName(basePakPath)!;
            var files = Directory.GetFiles(directory);
            foreach (var f in files)
            {
                var match = Regex.Match(Path.GetFileName(f), @"(^.*)\.patch_([0-9]{3})\.pak$");
                if (match.Success)
                {
                    var basePath = match.Groups[1].Value;
                    if (basePath != basePakFileName)
                        continue;

                    fileOrder.Add(f);
                }
            }

            fileOrder.Sort(StringComparer.OrdinalIgnoreCase);
            _collection = new PakFileCollection(fileOrder
                .Select(x => (IPakFile)new PakFile(x))
                .ToImmutableArray());
        }

        public void Dispose() => _collection.Dispose();
        public ImmutableArray<ulong> FileHashes => _collection.FileHashes;
        public byte[]? GetEntryData(ulong hash) => _collection.GetEntryData(hash);
        public byte[]? GetEntryData(string path) => _collection.GetEntryData(path);
    }
}
