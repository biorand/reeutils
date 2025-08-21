﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace IntelOrca.Biohazard.REE.Package
{
    public sealed class PatchedPakFile : IPakFile, IDisposable
    {
        private readonly ImmutableArray<PakFile> _files;

        public PatchedPakFile(params string[] paths)
        {
            var files = new List<PakFile>();
            foreach (var path in paths)
            {
                var match = Regex.Match(path, @"(^.*)\.patch_([0-9]{3})\.pak$");
                if (match.Success)
                {
                    var basePath = match.Groups[1].Value;
                    var endNumber = int.Parse(match.Groups[2].Value);
                    if (File.Exists(basePath))
                    {
                        files.Add(new PakFile(basePath));
                    }
                    for (var i = 1; i <= endNumber; i++)
                    {
                        var patchFileName = $"{basePath}.patch_{i:000}.pak";
                        if (File.Exists(patchFileName))
                        {
                            files.Add(new PakFile(patchFileName));
                        }
                    }
                }
                else
                {
                    files.Add(new PakFile(path));
                    for (var i = 1; i < 10000; i++)
                    {
                        var patchFileName = $"{path}.patch_{i:000}.pak";
                        if (File.Exists(patchFileName))
                        {
                            files.Add(new PakFile(patchFileName));
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            _files = [.. files];
        }

        public void Dispose()
        {
            foreach (var f in _files)
            {
                f.Dispose();
            }
        }

        public byte[]? GetEntryData(ulong hash)
        {
            for (var i = _files.Length - 1; i >= 0; i--)
            {
                var data = _files[i].GetEntryData(hash);
                if (data != null)
                {
                    return data;
                }
            }
            return null;
        }

        public byte[]? GetEntryData(string path)
        {
            for (var i = _files.Length - 1; i >= 0; i--)
            {
                var data = _files[i].GetEntryData(path);
                if (data != null)
                {
                    return data;
                }
            }
            return null;
        }

        public ImmutableArray<ulong> FileHashes => _files
            .SelectMany(x => x.FileHashes)
            .OrderBy(x => x)
            .ToImmutableArray();
    }
}
