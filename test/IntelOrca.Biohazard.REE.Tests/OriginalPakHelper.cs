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
                GameNames.RE3 => FindFirstExisting(
                    @"F:\games\steamapps\common\RE3"),
                GameNames.RE4 => FindFirstExisting(
                    @"G:\biorand\re4r\vanilla"),
                GameNames.RE7 => FindFirstExisting(
                    @"F:\games\steamapps\common\RESIDENT EVIL 7 biohazard"),
                GameNames.RE8 => FindFirstExisting(
                    @"G:\biorand\re8\vanilla"),
                GameNames.RE9 => FindFirstExisting(
                    @"F:\games\steamapps\common\RESIDENT EVIL requiem BIOHAZARD requiem"),
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

                var alt = NormalizePathForPlatform(p);
                if (!string.Equals(alt, p, StringComparison.Ordinal) && Directory.Exists(alt))
                {
                    return alt;
                }
            }
            throw new Exception("No defined path exists for this game.");
        }

        private static string NormalizePathForPlatform(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return path.Replace('/', '\\');
            }

            var p = path.Replace('\\', '/');
            if (p.Length >= 2 && p[1] == ':')
            {
                var drive = char.ToLowerInvariant(p[0]);
                var rest = p.Substring(2).TrimStart('/');
                return $"/mnt/{drive}/{rest}";
            }
            return p;
        }

        public RszTypeRepository GetTypeRepository(string gameName)
        {
            var jsonPath = gameName switch
            {
                GameNames.RE2 => @"M:\git\reasy\resources\data\dumps\rszre2.json",
                GameNames.RE3 => @"M:\git\reasy\resources\data\dumps\rszre3.json",
                GameNames.RE4 => @"M:\git\reasy\resources\data\dumps\rszre4.json",
                GameNames.RE7 => @"M:\git\reasy\resources\data\dumps\rszre7rt.json",
                GameNames.RE8 => @"M:\git\reasy\resources\data\dumps\rszre8.json",
                GameNames.RE9 => @"M:\git\reasy\resources\data\dumps\rszre9.json",
                _ => throw new NotImplementedException()
            };

            jsonPath = NormalizePathForPlatform(jsonPath);
            var json = File.ReadAllBytes(jsonPath);
            var repo = RszRepositorySerializer.Default.FromJson(json);
            return repo;
        }
    }
}
