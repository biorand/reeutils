using System;
using System.IO;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REEUtils.Commands;
using REE;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public class TestImportExport
    {
        private PatchedPakFile _pak;

        public TestImportExport()
        {
            _pak = GetVanillaPak();
        }

        [Theory]
        [InlineData("natives/stm/_chainsaw/appsystem/ui/userdata/itemcraftsettinguserdata.user.2")]
        public async Task ImportExportImport(string path)
        {
            using var tempFolder = new TempFolder();
            var userData = _pak.GetFileData(path) ?? throw new Exception();
            var userPath = tempFolder.GetSubPath("test.user.2");
            var jsonPath = tempFolder.GetSubPath("test.json");
            File.WriteAllBytes(userPath, userData);

            var exportCommand = new ExportCommand();
            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = userPath,
                Game = "re4",
                OutputPath = jsonPath
            });

            var jsonA = File.ReadAllText(jsonPath);

            var importCommand = new ImportCommand();
            await importCommand.ExecuteAsync(null!, new ImportCommand.Settings()
            {
                InputPath = jsonPath,
                Game = "re4",
                OutputPath = userPath
            });
            await exportCommand.ExecuteAsync(null!, new ExportCommand.Settings()
            {
                InputPath = userPath,
                Game = "re4",
                OutputPath = jsonPath
            });

            var jsonB = File.ReadAllText(jsonPath);

            Assert.Equal(jsonA, jsonB);
        }

        private PatchedPakFile GetVanillaPak()
        {
            var basePath = @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4";
            var patch3 = Path.Combine(basePath, "re_chunk_000.pak.patch_003.pak");
            return new PatchedPakFile(patch3);
        }
    }

    internal sealed class TempFolder : IDisposable
    {
        private bool _disposed;

        public string Path { get; }

        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public string GetSubPath(string relative)
        {
            return System.IO.Path.Combine(Path, relative);
        }

        ~TempFolder()
        {
            if (!_disposed)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}