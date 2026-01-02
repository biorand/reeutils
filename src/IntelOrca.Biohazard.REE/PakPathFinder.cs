using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE
{
    public sealed class PakPathFinder
    {
        private readonly RszTypeRepository _rszTypeRepository;
        private readonly IPakFile _pakFile;
        private readonly ImmutableHashSet<ulong> _allFileHashes;

        private readonly HashSet<string> _potentialPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public PakPathFinder(RszTypeRepository rszTypeRepository, IPakFile pakFile)
        {
            _rszTypeRepository = rszTypeRepository;
            _pakFile = pakFile;
            _allFileHashes = _pakFile.FileHashes.ToImmutableHashSet();
        }

        public ulong[] GetUnknownHashes(PakList excludeList)
        {
            return _allFileHashes.Except(excludeList.Hashes).ToArray();
        }

        public string[] Find(PakList excludeList)
        {
            foreach (var hash in _allFileHashes)
            {
                try
                {
                    var entry = _pakFile.GetEntryData(hash);
                    var fileKind = FileVersion.DetectFileKind(entry);
                    if (fileKind == FileKind.UserData)
                    {
                        var userFile = new UserFile(entry);
                        var objects = userFile.GetObjects(_rszTypeRepository);
                        foreach (var obj in objects)
                        {
                            Visit(obj);
                        }
                    }
                    else if (fileKind == FileKind.Scene)
                    {
                        var scnFile = new ScnFile(20, entry);
                        DiscoverStrings(scnFile.Resources);
                        var scene = scnFile.ReadScene(_rszTypeRepository);
                        Visit(scene);
                    }
                    else if (fileKind == FileKind.Prefab)
                    {
                        var pfbFile = new PfbFile(17, entry);
                        var scene = pfbFile.ReadScene(_rszTypeRepository);
                        DiscoverStrings(pfbFile.Resources);
                        Visit(scene);
                    }
                }
                catch
                {
                    Console.WriteLine($"Failed to look at {hash}");
                }
            }

            var foundPaths = new List<string>();
            foreach (var p in _potentialPaths)
            {
                var fullPath = GetFullPath(p);
                var hash = PakFile.GetNormalizedPathHash(fullPath);
                if (excludeList.GetPath(hash) != null)
                    continue;
                if (!_allFileHashes.Contains(hash))
                {
                    // Console.WriteLine(">>>>" + fullPath);
                    continue;
                }
                foundPaths.Add(fullPath);
            }
            return foundPaths
                .OrderBy(x => x)
                .ToArray();
        }

        private static string GetFullPath(string p)
        {
            foreach (var ext in g_extensions)
            {
                var versionStart = ext.LastIndexOf('.');
                var extension = ext.Substring(0, versionStart);
                var version = ext.Substring(versionStart);
                if (p.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return $"natives/stm/{p}{version}";
            }
            return $"natives/stm/{p}";
        }

        private void DiscoverStrings(IEnumerable<string> strings)
        {
            foreach (var s in strings)
            {
                DiscoverString(s);
            }
        }

        private void DiscoverString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return;
            if (s.StartsWith("_", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("/") || s.Contains("\\"))
            {
                _potentialPaths.Add(s);
            }
        }

        private void Visit(IRszNode node)
        {
            if (node is RszStringNode stringNode)
            {
                DiscoverString(stringNode.Value);
            }
            else if (node is RszResourceNode resourceNode)
            {
                DiscoverString(resourceNode.Value ?? "");
            }
            else if (node is RszUserDataNode userDataNode)
            {
                DiscoverString(userDataNode.Path ?? "");
            }

            if (node is IRszNodeContainer container)
            {
                if (node is RszFolder folder)
                {
                    Visit(folder.Settings);
                }
                foreach (var child in container.Children)
                {
                    Visit(child);
                }
            }
        }

        private static readonly string[] g_extensions = new[] {
            ".cfil.7",
            ".chain.53",
            ".efx.3539837",
            ".lfa.4",
            ".mcol.20021",
            ".mdf2.32",
            ".mesh.221108797",
            ".mot.613",
            ".motbank.3",
            ".motfsm2.43",
            ".msg.22",
            ".pfb.17",
            ".rbs.34",
            ".rcol.25",
            ".rmesh.24013",
            ".scn.20",
            ".tex.143221013",
            ".user.2",
        };
    }
}
