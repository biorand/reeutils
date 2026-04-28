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
            var rszDir = GetEnvironmentVariable("REEUTILS_RSZ_DIR",
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? @"M:\git\reasy\resources\data\dumps"
                    : @"/mnt/m/git/reasy/resources/data/dumps");
            var jsonPath = gameName switch
            {
                GameNames.RE2 => "rszre2.json",
                GameNames.RE3 => "rszre3.json",
                GameNames.RE4 => "rszre4.json",
                GameNames.RE7 => "rszre7rt.json",
                GameNames.RE8 => "rszre8.json",
                GameNames.RE9 => "rszre9.json",
                _ => throw new NotImplementedException()
            };
            var json = File.ReadAllBytes(Path.Combine(rszDir, jsonPath));
            var repo = RszRepositorySerializer.Default.FromJson(json);
            return repo;
        }

        public PakList GetPakList(string gameName)
        {
            var paklistFileName = $"paklist.{gameName.ToLowerInvariant()}.txt.gz";
            var dataPath = GetEnvironmentVariable("REEUTILS_PAKLIST_DIR",
                 Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../src/reeutils/data")));
            return PakList.FromFile(Path.Combine(dataPath, paklistFileName));
        }

        private static string GetEnvironmentVariable(string name, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }
}
