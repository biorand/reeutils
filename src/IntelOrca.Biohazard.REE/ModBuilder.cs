using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REE
{
    /// <summary>
    /// A builder for creating and packaging mod files.
    /// Supports generating PAK and Fluffy ZIP formats with metadata and file entries.
    /// </summary>
    public class ModBuilder
    {
        private readonly Dictionary<string, byte[]> _entries;

        public string? Name { get; set; } = "Unnamed Mod";
        public string? Version { get; set; } = "1.0";
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Category { get; set; } = "!Other > Misc";
        public string? ScreenshotFileName { get; set; }
        public byte[]? ScreenshotFileContent { get; set; }

        public ImmutableDictionary<string, byte[]> Entries => _entries.ToImmutableDictionary();

        public ModBuilder()
        {
            _entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        }

        public byte[]? this[string key]
        {
            get => _entries.GetValueOrDefault(key);
            set
            {
                if (value == null)
                    _entries.Remove(key);
                else
                    _entries[key] = value;
            }
        }

        public bool ContainsFile(string key) => _entries.ContainsKey(key);

        public ModBuilder AddFile(string key, ReadOnlyMemory<byte> value) => AddFile(key, value.Span);
        public ModBuilder AddFile(string key, ReadOnlySpan<byte> value) => AddFile(key, value.ToArray());
        public ModBuilder AddFile(string key, byte[] value)
        {
            this[key] = value;
            return this;
        }

        public void AddZipContents(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            AddZipContents(fs);
        }

        public void AddZipContents(Stream stream)
        {
            using var zipFile = new ZipArchive(stream, ZipArchiveMode.Read, true);
            foreach (var entry in zipFile.Entries)
            {
                if (entry.Length != 0)
                {
                    using var ms = new MemoryStream();
                    using var entryStream = entry.Open();
                    entryStream.CopyTo(ms);
                    AddFile(entry.FullName, ms.ToArray());
                }
            }
        }

        public void SavePakFile(string path) => File.WriteAllBytes(path, BuildPakFile());
        public void SaveFluffyZipFile(string path) => File.WriteAllBytes(path, BuildFluffyZipFile());
        public void SaveFluffyFolder(string path, bool deleteIfExists = false)
        {
            if (Directory.Exists(path))
            {
                if (deleteIfExists)
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    throw new ArgumentException($"'{path}' already exists.");
                }
            }

            Directory.CreateDirectory(path);

            var extraFiles = GetExtraFluffyFiles();
            foreach (var f in extraFiles)
            {
                WriteEntry(f);
            }

            foreach (var entry in _entries)
            {
                WriteEntry(entry);
            }

            void WriteEntry(KeyValuePair<string, byte[]> entry)
            {
                var fullPath = Path.Combine(path, entry.Key);
                var directory = Path.GetDirectoryName(fullPath)!;
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(fullPath, entry.Value);
            }
        }

        public byte[] BuildPakFile()
        {
            var pakFileBuilder = new PakFileBuilder();
            foreach (var entry in _entries)
            {
                pakFileBuilder.AddEntry(entry.Key, entry.Value);
            }
            return pakFileBuilder.ToByteArray();
        }

        public byte[] BuildFluffyZipFile()
        {
            var zipFileBuilder = new ZipFileBuilder();

            var extraFiles = GetExtraFluffyFiles();
            foreach (var f in extraFiles)
            {
                zipFileBuilder.AddEntry(f.Key, f.Value);
            }

            foreach (var entry in _entries)
            {
                zipFileBuilder.AddEntry(entry.Key, entry.Value);
            }

            return zipFileBuilder.Build();
        }

        public ImmutableDictionary<string, byte[]> GetExtraFluffyFiles()
        {
            var result = ImmutableDictionary.CreateBuilder<string, byte[]>();
            if (ScreenshotFileName != null && ScreenshotFileContent != null)
            {
                result.Add(ScreenshotFileName, ScreenshotFileContent);
            }
            result.Add("modinfo.ini", GetFluffyModInfoIniContent());
            return result.ToImmutable();
        }

        private byte[] GetFluffyModInfoIniContent()
        {
            var dict = new List<(string, string?)>();
            dict.Add(("name", Name));
            dict.Add(("version", Version));
            dict.Add(("description", Description));
            dict.Add(("screenshot", ScreenshotFileName));
            dict.Add(("author", Author));
            dict.Add(("category", Category));

            var sb = new StringBuilder();
            foreach (var (key, value) in dict)
            {
                if (value == null)
                    continue;

                sb.Append(key);
                sb.Append(Sanitize(value));
                sb.Append("\n");
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string Sanitize(string? s)
        {
            return (s ?? "").Trim().Replace("\r\n", "\\n");
        }
    }
}
