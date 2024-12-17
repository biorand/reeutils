using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.REE.Compression;
using IntelOrca.Biohazard.REE.Cryptography;

namespace IntelOrca.Biohazard.REE.Package
{
    public class PakFileBuilder
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, object> Entries => _entries;

        public void AddEntry(string path, byte[] data)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (data == null) throw new ArgumentNullException(nameof(data));

            _entries[path] = data;
        }

        public void AddEntry(string path, string srcPath)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            _entries[path] = srcPath;
        }

        public void AddDirectory(string path, string pakRootPath = "")
        {
            if (pakRootPath.Length != 0 && !pakRootPath.EndsWith("/"))
                pakRootPath += '/';

            var entries = Directory.GetFiles(path);
            foreach (var entry in entries)
            {
                var pakPath = pakRootPath + Path.GetFileName(entry);
                AddEntry(pakPath, entry);
            }
            foreach (var directory in Directory.GetDirectories(path))
            {
                var pakPath = pakRootPath + Path.GetFileName(directory);
                AddDirectory(directory, pakPath);
            }
        }

        public void Save(string path, CompressionKind CompressionKind)
        {
            using var stream = File.OpenWrite(path);
            Save(stream, CompressionKind);
        }

        public void Save(Stream stream, CompressionKind CompressionKind)
        {
            var header = new PakFile.Header
            {
                wMagic = PakFile.g_magic,
                MajorVersion = 4,
                TotalFiles = _entries.Count,
                Hash = 0xDEC0ADDE
            };

            var pakEntries = new PakFile.Entry[header.TotalFiles];
            using var bw = new BinaryWriter(stream);

            // Write pak header
            bw.Write(header.wMagic);
            bw.Write(header.MajorVersion);
            bw.Write(header.MinorVersion);
            bw.Write(header.Feature);
            bw.Write(header.TotalFiles);
            bw.Write(header.Hash);
            bw.Seek(header.TotalFiles * 48, SeekOrigin.Current);

            // Write entries
            var index = 0;
            foreach (var entry in _entries)
            {
                var entryPath = entry.Key;
                var entryData = entry.Value is string s ? File.ReadAllBytes(s) : (byte[])entry.Value;

                var pakEntry = new PakFile.Entry();
                string pakEntryPath;
                if (entryPath.Contains("__Unknown"))
                {
                    pakEntryPath = Path.GetFileNameWithoutExtension(entryPath);
                    pakEntry.HashName = Convert.ToUInt64(pakEntryPath, 16);
                }
                else
                {
                    pakEntryPath = entryPath.Replace("\\", "/");
                    pakEntry.HashNameUpper = PakFile.Hash32(pakEntryPath.ToUpperInvariant());
                    pakEntry.HashNameLower = PakFile.Hash32(pakEntryPath.ToLowerInvariant());
                }

                pakEntry.Offset = bw.BaseStream.Position;
                pakEntry.Checksum = Crc64.HashData(entryData);

                if (entryData.Length >= 8)
                {
                    var magic1 = BitConverter.ToUInt32(entryData, 0);
                    var magic2 = BitConverter.ToUInt32(entryData, 4);

                    // mov, bnk, pck must be uncompressed
                    if (magic1 == 0x75B22630 || magic1 == 0x564D4552 || magic1 == 0x44484B42 || magic1 == 0x4B504B41 || magic2 == 0x70797466)
                    {
                        bw.Write(entryData);

                        pakEntry.CompressedSize = entryData.Length;
                        pakEntry.DecompressedSize = entryData.Length;
                        pakEntry.CompressionType = (byte)CompressionKind.None;
                    }
                    else
                    {
                        var buffer = CompressionKind switch
                        {
                            CompressionKind.Deflate => Deflate.CompressData(entryData),
                            CompressionKind.Zstd => Zstd.CompressData(entryData),
                            _ => throw new ArgumentException("CompressionKind not supported")
                        };
                        bw.Write(buffer);

                        pakEntry.CompressedSize = buffer.Length;
                        pakEntry.DecompressedSize = entryData.Length;
                        pakEntry.CompressionType = (byte)CompressionKind;
                    }
                }
                else
                {
                    pakEntry.CompressedSize = entryData.Length;
                    pakEntry.DecompressedSize = entryData.Length;
                    pakEntry.CompressionType = (byte)CompressionKind.None;

                    bw.Write(entryData);
                }

                pakEntries[index] = pakEntry;
                index++;
            }

            // Write entry table
            bw.Seek(16, SeekOrigin.Begin);
            var sortedPakEntries = pakEntries.OrderBy(e => e.HashName).ToArray();
            foreach (var pakEntry in sortedPakEntries)
            {
                bw.Write(pakEntry.HashName);
                bw.Write(pakEntry.Offset);
                bw.Write(pakEntry.CompressedSize);
                bw.Write(pakEntry.DecompressedSize);
                bw.Write(pakEntry.CompressionType);
                bw.Write(pakEntry.CompressionFlags);
                bw.Write(pakEntry.EncryptionType);
                bw.Write(pakEntry.EncryptionFlags);
                bw.Write(pakEntry.Reserved);
                bw.Write(pakEntry.Checksum);
            }
        }

        public byte[] ToByteArray()
        {
            var ms = new MemoryStream();
            Save(ms, CompressionKind.Zstd);
            return ms.ToArray();
        }

        public PakFile ToPakFile()
        {
            return new PakFile(ToByteArray());
        }
    }
}
