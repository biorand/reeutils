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

        public string[] Find(PakList excludeList)
        {
            foreach (var hash in _allFileHashes)
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
            }

            var foundPaths = new List<string>();
            foreach (var p in _potentialPaths)
            {
                var fullPath = GetFullPath(p);
                var hash = PakFile.GetNormalizedPathHash(fullPath);
                if (excludeList.GetPath(hash) != null)
                    continue;
                if (!_allFileHashes.Contains(hash))
                    continue;

                foundPaths.Add(fullPath);
            }
            return foundPaths
                .OrderBy(x => x)
                .ToArray();
        }

        private string GetFullPath(string p)
        {
            if (p.EndsWith(".pfb", StringComparison.OrdinalIgnoreCase))
                return $"natives/stm/{p}.17";
            else if (p.EndsWith(".scn", StringComparison.OrdinalIgnoreCase))
                return $"natives/stm/{p}.20";
            else if (p.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
                return $"natives/stm/{p}.user";
            return $"natives/stm/{p}";
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
                DiscoverString(resourceNode.Value);
            }
            else if (node is RszUserDataNode userDataNode)
            {
                DiscoverString(userDataNode.Path);
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }
    }
}
