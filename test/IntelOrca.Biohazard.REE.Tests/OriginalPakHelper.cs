using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REE.Tests
{
    /// <summary>
    /// Common functionality for getting the game PAK files for use in testing.
    /// </summary>
    internal sealed class OriginalPakHelper : IDisposable
    {
        private readonly Dictionary<string, PatchedPakFile> _pakFiles = [];
        private readonly object _sync = new object();

        public void Dispose()
        {
            foreach (var p in _pakFiles)
            {
                p.Value.Dispose();
            }
        }

        public byte[] GetFileData(string game, string path)
        {
            var pak = GetPatchedPak(game);
            return pak.GetFileData(path) ?? throw new FileNotFoundException($"{path} not found", path);
        }

        private PatchedPakFile GetPatchedPak(string game)
        {
            lock (_sync)
            {
                if (!_pakFiles.TryGetValue(game, out var result))
                {
                    var dir = GetInstallPath(game);
                    var basePath = Path.Combine(dir, "re_chunk_000.pak");
                    result = new PatchedPakFile(basePath);
                    _pakFiles[game] = result;
                }
                return result;
            }
        }

        public string GetInstallPath(string game)
        {
            return game switch
            {
                GameNames.RE4 => FindFirstExisting(
                    @"G:\re4r\vanilla"),
                _ => throw new NotSupportedException()
            };
        }

        private static string FindFirstExisting(params string[] paths)
        {
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    return p;
                }
            }
            throw new Exception("No defined path exists for this game.");
        }
    }
}
