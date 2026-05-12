using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using ModelContextProtocol;

namespace IntelOrca.Biohazard.REEUtils
{
    internal sealed class McpSession : IDisposable
    {
        private IPakFile? _pak;

        public IPakFile? Pak => _pak;
        public string? PakPath { get; private set; }
        public PakList? PakList { get; private set; }
        public string? PakListSource { get; private set; }
        public RszTypeRepository? RszTypeRepository { get; private set; }
        public string? RszSource { get; private set; }
        public string? Game { get; private set; }

        public void OpenPak(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new McpException($"Pak path '{path}' was not found.");

            IPakFile pak = File.Exists(path)
                ? new PakFile(path)
                : new RePakCollection(path);

            ReplacePak(pak, path);
        }

        public void OpenRsz(string path)
        {
            RszTypeRepository = McpEmbeddedData.LoadRszTypeRepositoryFromPath(path);
            RszSource = path;
            Game = null;
        }

        public void OpenPakList(string path)
        {
            PakList = McpEmbeddedData.LoadPakListFromPath(path);
            PakListSource = path;
            Game = null;
        }

        public void SetGame(string game)
        {
            if (string.IsNullOrWhiteSpace(game))
                throw new McpException("Game must be specified.");

            var normalizedGame = game.Trim().ToLowerInvariant();
            var previousGame = Game;

            RszTypeRepository = McpEmbeddedData.GetRszTypeRepository(normalizedGame);
            RszSource = $"embedded:rsz{normalizedGame}.json.gz";
            PakList = McpEmbeddedData.GetPakList(normalizedGame);
            PakListSource = $"embedded:paklist.{normalizedGame}.txt.gz";
            Game = normalizedGame;

            if (_pak != null &&
                previousGame != null &&
                !string.Equals(previousGame, normalizedGame, StringComparison.OrdinalIgnoreCase))
            {
                ClearPak();
            }
        }

        public byte[] ReadFileData(string path, out string resolvedPath)
        {
            if (File.Exists(path))
            {
                resolvedPath = path;
                return File.ReadAllBytes(path);
            }

            var pak = _pak ?? throw new McpException("No pak is open. Call open_pak first.");
            resolvedPath = ResolvePakPath(path);
            return pak.GetEntryData(resolvedPath) ?? throw new McpException($"File '{resolvedPath}' was not found in the current pak.");
        }

        public string ResolvePakPath(string path)
        {
            var pak = _pak ?? throw new McpException("No pak is open. Call open_pak first.");
            if (string.IsNullOrWhiteSpace(path))
                throw new McpException("A pak path must be specified.");

            var normalized = path.Replace('\\', '/').Trim();
            foreach (var candidate in GetPakPathCandidates(normalized))
            {
                if (pak.GetEntryData(candidate) != null)
                    return candidate;
            }

            if (PakList != null)
            {
                var candidates = new HashSet<string>(GetPakPathCandidates(normalized), StringComparer.OrdinalIgnoreCase);
                foreach (var entry in PakList.Entries)
                {
                    if (candidates.Contains(entry) ||
                        string.Equals(FileHandlerFactory.Default.GetReferencePath(entry), normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }
                }
            }

            throw new McpException($"File '{path}' was not found in the current pak.");
        }

        public object GetStatus()
        {
            return new
            {
                game = Game,
                pakPath = PakPath,
                pakListSource = PakListSource,
                rszSource = RszSource,
                pakLoaded = _pak != null,
                pakListLoaded = PakList != null,
                rszLoaded = RszTypeRepository != null
            };
        }

        public void Dispose()
        {
            ClearPak();
        }

        private void ReplacePak(IPakFile pak, string path)
        {
            var previous = _pak;
            _pak = pak;
            PakPath = path;
            previous?.Dispose();
        }

        private void ClearPak()
        {
            _pak?.Dispose();
            _pak = null;
            PakPath = null;
        }

        private static IEnumerable<string> GetPakPathCandidates(string path)
        {
            yield return path;

            var fullPath = FileHandlerFactory.Default.GetFullPathFromArg(path);
            if (!string.Equals(fullPath, path, StringComparison.OrdinalIgnoreCase))
                yield return fullPath;
        }
    }
}
