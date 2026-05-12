using System.Runtime.InteropServices;
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
        private readonly Dictionary<string, RszTypeRepository> _typeRepositories = [];
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

        public string GetPakFilePath(string game, params string[] relativePath)
        {
            var fullPath = Path.Combine([GetInstallPath(game), .. relativePath]);
            if (!File.Exists(fullPath))
            {
                Assert.Skip($"Skipping because required game pak '{fullPath}' was not found.");
            }
            return fullPath;
        }

        private IPakFile GetPatchedPak(string game)
        {
            lock (_sync)
            {
                if (!_pakFiles.TryGetValue(game, out var result))
                {
                    var dir = GetInstallPath(game);
                    var basePakPath = Path.Combine(dir, "re_chunk_000.pak");
                    if (!File.Exists(basePakPath))
                    {
                        Assert.Skip($"Skipping because required game pak '{basePakPath}' was not found.");
                    }
                    result = new RePakCollection(dir);
                    _pakFiles[game] = result;
                }
                return result;
            }
        }

        public string GetInstallPath(string game)
        {
            var streamDir = GetEnvironmentVariable("STEAM_DIR",
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? @"F:\games\steamapps\common"
                    : @"/mnt/f/games/steamapps/common");
            var gameDirName = game switch
            {
                GameNames.RE2 => "RESIDENT EVIL 2  BIOHAZARD RE2",
                GameNames.RE3 => "RE3",
                GameNames.RE4 => "RESIDENT EVIL 4  BIOHAZARD RE4",
                GameNames.RE7 => "RESIDENT EVIL 7 biohazard",
                GameNames.RE8 => "Resident Evil Village BIOHAZARD VILLAGE",
                GameNames.RE9 => "RESIDENT EVIL requiem BIOHAZARD requiem",
                _ => throw new NotSupportedException()
            };
            return Path.Combine(streamDir, gameDirName);
        }

        public RszTypeRepository GetTypeRepository(string gameName)
        {
            lock (_sync)
            {
                if (_typeRepositories.TryGetValue(gameName, out var repo))
                {
                    return repo;
                }

                var baseName = gameName switch
                {
                    GameNames.RE2 => "rszre2",
                    GameNames.RE3 => "rszre3",
                    GameNames.RE4 => "rszre4",
                    GameNames.RE7 => "rszre7",
                    GameNames.RE8 => "rszre8",
                    GameNames.RE9 => "rszre9",
                    _ => throw new NotImplementedException()
                };
                var externalDir = GetEnvironmentVariable("REEUTILS_RSZ_DIR",
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? @"M:\git\reasy\resources\data\dumps"
                        : @"/mnt/m/git/reasy/resources/data/dumps");
                var repoDataDir = GetRepoDataPath();
                var compressedPath = Path.Combine(repoDataDir, $"{baseName}.json.gz");
                var jsonPath = Path.Combine(externalDir, $"{baseName}.json");

                if (File.Exists(compressedPath))
                {
                    repo = RszRepositorySerializer.Default.FromJsonGz(File.ReadAllBytes(compressedPath));
                }
                else if (File.Exists(jsonPath))
                {
                    repo = RszRepositorySerializer.Default.FromJson(File.ReadAllBytes(jsonPath));
                }
                else
                {
                    Assert.Skip($"Skipping because required RSZ repository data for '{gameName}' was not found.");
                    throw new InvalidOperationException();
                }

                _typeRepositories[gameName] = repo;
                return repo;
            }
        }

        public PakList GetPakList(string gameName)
        {
            var paklistFileName = $"paklist.{gameName.ToLowerInvariant()}.txt.gz";
            var dataPath = GetEnvironmentVariable("REEUTILS_PAKLIST_DIR",
                 GetRepoDataPath());
            var pakListPath = Path.Combine(dataPath, paklistFileName);
            if (!File.Exists(pakListPath))
            {
                Assert.Skip($"Skipping because required pak list '{pakListPath}' was not found.");
            }
            return PakList.FromFile(pakListPath);
        }

        private static string GetEnvironmentVariable(string name, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static string GetRepoDataPath()
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../src/reeutils/data"));
        }
    }
}
