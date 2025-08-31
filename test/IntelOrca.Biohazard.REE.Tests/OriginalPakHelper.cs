using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    /// <summary>
    /// Common functionality for getting the game PAK files for use in testing.
    /// </summary>
    internal sealed class OriginalPakHelper : IDisposable
    {
        private readonly Dictionary<string, IPakFile> _pakFiles = [];
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
            return pak.GetEntryData(path) ?? throw new FileNotFoundException($"{path} not found", path);
        }

        private IPakFile GetPatchedPak(string game)
        {
            lock (_sync)
            {
                if (!_pakFiles.TryGetValue(game, out var result))
                {
                    var dir = GetInstallPath(game);
                    result = new RePakCollection(dir);
                    _pakFiles[game] = result;
                }
                return result;
            }
        }

        public string GetInstallPath(string game)
        {
            return game switch
            {
                GameNames.RE2 => FindFirstExisting(
                    @"F:\games\steamapps\common\RESIDENT EVIL 2  BIOHAZARD RE2"),
                GameNames.RE4 => FindFirstExisting(
                    @"G:\re4r\vanilla"),
                GameNames.RE8 => FindFirstExisting(
                    @"G:\biorand\re8\vanilla"),
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

        public RszTypeRepository GetTypeRepository(string gameName)
        {
            var jsonPath = gameName switch
            {
                GameNames.RE2 => @"M:\git\reasy\resources\data\dumps\rszre2.json",
                GameNames.RE4 => @"M:\git\reasy\resources\data\dumps\rszre4.json",
                GameNames.RE8 => @"M:\git\reasy\resources\data\dumps\rszre8.json",
                _ => throw new NotImplementedException()
            };
            var json = File.ReadAllBytes(jsonPath);
            var repo = RszRepositorySerializer.Default.FromJson(json);
            return repo;
        }
    }
}
