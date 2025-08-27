using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace IntelOrca.Biohazard.REE.Package
{
    public sealed class RePakCollection : IPakFile, IDisposable
    {
        private const string BasePakFileName = "re_chunk_000.pak";

        private readonly PakFileCollection _collection;

        public RePakCollection(string gamePath)
        {
            var basePakPath = Path.Combine(gamePath, BasePakFileName);
            if (!File.Exists(basePakPath))
                throw new Exception($"Failed to find {BasePakFileName}.");

            var patchedMainPak = new PatchedPakFile(basePakPath);
            var dlcPaks = new List<IPakFile>();

            foreach (var directory in Directory.GetDirectories(gamePath))
            {
                foreach (var f in Directory.GetFiles(directory))
                {
                    var fileName = Path.GetFileName(f);
                    if (fileName.StartsWith("re_dlc_", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
                    {
                        dlcPaks.Add(new PakFile(f));
                    }
                }
            }
            _collection = new PakFileCollection([patchedMainPak, .. dlcPaks]);
        }

        public void Dispose() => _collection.Dispose();
        public ImmutableArray<ulong> FileHashes => _collection.FileHashes;
        public byte[]? GetEntryData(ulong hash) => _collection.GetEntryData(hash);
        public byte[]? GetEntryData(string path) => _collection.GetEntryData(path);
    }
}
