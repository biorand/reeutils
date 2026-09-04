using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REEUtils;
using IntelOrca.Biohazard.REEUtils.Commands;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    /// <summary>
    /// RE2 (non-RT) JSON roundtrip tests: export -> import -> export must produce identical
    /// JSON, and the re-imported file must be byte-identical to the original.
    /// </summary>
    public class TestRe2ImportExport : IDisposable
    {
        private const string Game = "re2";

        private RePakCollection _pak;

        public TestRe2ImportExport()
        {
            _pak = GetVanillaPak();
        }

        public void Dispose()
        {
            _pak.Dispose();
        }

        [Theory]
        [InlineData("natives/x64/objectroot/setmodel/sm4x_gimmick/sm49/sm49_315_tylantbreakwall/sm49_315_tylantbrakewall.pfb.16")]
        [InlineData("natives/x64/objectroot/prefab/character/enemydead/em0000_dead.pfb.16")]
        public async Task PfbFile16(string path)
        {
            await CheckFileAsync(path, ".pfb.16");
        }

        [Theory]
        [InlineData("natives/x64/objectroot/scene/location/rpd/level_100/environments/st4_701_0/gimmick.scn.19")]
        public async Task ScnFile19(string path)
        {
            await CheckFileAsync(path, ".scn.19");
        }

        [Theory]
        [InlineData("natives/x64/objectroot/setmodel/control/test_yamakawa/lab_tyrantoff.fsmv2.30")]
        public async Task Fsmv2File30(string path)
        {
            await CheckFileAsync(path, ".fsmv2.30");
        }

        /// <summary>
        /// Full-corpus check: every .fsmv2.30 in the vanilla install must roundtrip through JSON
        /// byte-identically. Skips when no vanilla install is present (see GetVanillaPak).
        /// </summary>
        [Fact]
        public async Task Fsmv2File30_AllCorpus()
        {
            var pakList = EmbeddedData.GetPakList(Game)
                ?? throw new Exception("Embedded pak list for re2 not found.");
            var paths = new List<string>();
            foreach (var h in _pak.FileHashes)
            {
                var pth = pakList.GetPath(h);
                if (pth != null && pth.EndsWith(".fsmv2.30")) paths.Add(pth);
            }
            Assert.True(paths.Count > 0, "No fsmv2.30 files found in the vanilla pak.");

            var failures = new List<string>();
            foreach (var path in paths)
            {
                try
                {
                    await CheckFileAsync(path, ".fsmv2.30");
                }
                catch (Exception ex)
                {
                    failures.Add($"{path}: {ex.Message}");
                }
            }
            Assert.True(failures.Count == 0,
                $"{failures.Count}/{paths.Count} fsmv2.30 files failed roundtrip:\n{string.Join("\n", failures)}");
        }

        /// <summary>
        /// Full-corpus check: every .scn.19 in the vanilla install must roundtrip through JSON
        /// byte-identically.
        /// </summary>
        [Fact]
        public async Task ScnFile19_AllCorpus()
        {
            await CheckCorpus(".scn.19");
        }

        /// <summary>
        /// Full-corpus check: every .pfb.16 in the vanilla install must roundtrip through JSON
        /// byte-identically.
        /// </summary>
        [Fact]
        public async Task PfbFile16_AllCorpus()
        {
            await CheckCorpus(".pfb.16");
        }

        private async Task CheckCorpus(string extension)
        {
            var pakList = EmbeddedData.GetPakList(Game)
                ?? throw new Exception("Embedded pak list for re2 not found.");
            var paths = new List<string>();
            foreach (var h in _pak.FileHashes)
            {
                var pth = pakList.GetPath(h);
                if (pth != null && pth.EndsWith(extension)) paths.Add(pth);
            }
            Assert.True(paths.Count > 0, $"No {extension} files found in the vanilla pak.");

            var failures = new List<string>();
            foreach (var path in paths)
            {
                try
                {
                    await CheckFileAsync(path, extension);
                }
                catch (Exception ex)
                {
                    failures.Add($"{path}: {ex.Message}");
                }
            }
            Assert.True(failures.Count == 0,
                $"{failures.Count}/{paths.Count} {extension} files failed roundtrip:\n{string.Join("\n", failures)}");
        }

        private async Task CheckFileAsync(string path, string extension)
        {
            using var tempFolder = new TempFolder();
            var entryData = _pak.GetEntryData(path) ?? throw new Exception($"'{path}' not found in vanilla pak.");
            var filePath = tempFolder.GetSubPath($"test{extension}");
            var jsonPath = tempFolder.GetSubPath("test.json");
            File.WriteAllBytes(filePath, entryData);

            var exportCommand = new ExportCommand();
            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = filePath,
                Game = Game,
                OutputPath = jsonPath
            });

            var jsonA = File.ReadAllText(jsonPath);

            var importCommand = new ImportCommand();
            await importCommand.ExecuteAsync(null!, new ImportCommand.Settings()
            {
                InputPath = jsonPath,
                Game = Game,
                OutputPath = filePath
            });

            var importedBytes = await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal(entryData, importedBytes);

            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = filePath,
                Game = Game,
                OutputPath = jsonPath
            });
            var jsonB = File.ReadAllText(jsonPath);
            Assert.Equal(jsonA, jsonB);
        }

        private RePakCollection GetVanillaPak()
        {
            var availablePaths = new string[]
            {
                @"E:\Steam\steamapps\common\RESIDENT EVIL 2  BIOHAZARD RE2",
                @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 2  BIOHAZARD RE2"
            };
            var basePath = availablePaths.FirstOrDefault(Directory.Exists);
            if (basePath == null)
            {
                Assert.Skip("Skipping because a vanilla RE2 install was not found.");
            }
            return new RePakCollection(basePath);
        }
    }
}
