using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Compression;
using IntelOrca.Biohazard.REE.Cryptography;

namespace IntelOrca.Biohazard.REE.Package
{
    public class PakFile : IPakFile, IDisposable
    {
        internal const uint g_magic = 0x414B504B;
        internal const uint g_zstd = 0xFD2FB528;

        private readonly Stream _stream;
        private readonly BinaryReader _br;
        private Header _header;
        private ImmutableArray<Entry> _entries = [];
        private ImmutableDictionary<ulong, int> _hashToEntry = ImmutableDictionary<ulong, int>.Empty;

        public PakFile(string path) : this(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
        }

        public PakFile(byte[] data) : this(new MemoryStream(data))
        {
        }

        public PakFile(Stream stream)
        {
            _stream = stream;
            _br = new BinaryReader(stream);
            if (stream.Length == 0)
                return;

            _header = ReadHeader(_br);

            if (_header.wMagic != g_magic)
                throw new InvalidDataException("Invalid PAK file");

            if (!SupportsVersion(_header.MajorVersion, _header.MinorVersion))
                throw new InvalidDataException($"Unsupported PAK version: {_header.MajorVersion}.{_header.MinorVersion}");

            if (_header.Feature != 0 && _header.Feature != 8)
                throw new InvalidDataException("Unsupported PAK encryption");

            _entries = ReadEntries(in _header, _br);

            var dict = new Dictionary<ulong, int>();
            for (var i = 0; i < _entries.Length; i++)
            {
                dict[_entries[i].HashName] = i;
            }
            _hashToEntry = dict.ToImmutableDictionary();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public void ExtractAll(PakList list, string destinationPath)
        {
            ExtractAllAsync(list, destinationPath).Wait();
        }

        public async Task ExtractAllAsync(PakList list, string destinationPath, CancellationToken ct = default)
        {
            foreach (var entry in _entries)
            {
                ct.ThrowIfCancellationRequested();

                var fileName = list.GetPath(entry.HashName);
                if (fileName == null)
                    continue;

                var fullPath = System.IO.Path.Combine(destinationPath, fileName!);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                var entryData = GetEntryData(in entry);
                if (fullPath.Contains("__Unknown"))
                {
                    var ext = ExtensionCalculator.DetectExtension(entryData);
                    if (ext != null)
                    {
                        fullPath += ext;
                    }
                }
#if NET
                await File.WriteAllBytesAsync(fullPath, entryData, ct);
#else
                using var fs = new FileStream(fullPath, FileMode.Create);
                await fs.WriteAsync(entryData, 0, entryData.Length);
#endif
            }
        }

        public int EntryCount => _entries.Length;

        public ImmutableArray<ulong> FileHashes => _hashToEntry.Keys
            .OrderBy(x => x)
            .ToImmutableArray();

        public ulong GetEntryHash(int index)
        {
            return _entries[index].HashName;
        }

        public string? GetEntryName(int index, PakList pakList)
        {
            var hash = _entries[index].HashName;
            return pakList.GetPath(hash);
        }

        private int FindEntry(string path)
        {
            var hash = GetNormalizedPathHash(path);
            return !_hashToEntry.TryGetValue(hash, out var index) ? -1 : index;
        }

        public byte[]? GetEntryData(ulong hash)
        {
            if (_hashToEntry.TryGetValue(hash, out var index))
                return GetEntryData(index);
            return null;
        }

        public byte[]? GetEntryData(string path)
        {
            var index = FindEntry(path);
            if (index == -1)
                return null;

            return GetEntryData(index);
        }

        public byte[] GetEntryData(int index)
        {
            return GetEntryData(_entries[index]);
        }

        private byte[] GetEntryData(in Entry entry)
        {
            _stream.Seek(entry.Offset, SeekOrigin.Begin);

            var compressionType = (CompressionKind)(entry.CompressionType & 0x0F);
            if (compressionType == CompressionKind.None)
            {
                return ReadBytes(entry.Offset, entry.CompressedSize);
            }
            else if (compressionType == CompressionKind.Deflate || compressionType == CompressionKind.Zstd)
            {
                var src = ReadBytes(entry.Offset, entry.CompressedSize);
                if (entry.EncryptionType > 0)
                {
                    src = ResourceCipher.DecryptData(src);
                }

                var magic = BitConverter.ToUInt32(src, 0);
                return magic == g_zstd
                    ? Zstd.DecompressData(src)
                    : Deflate.DecompressData(src);
            }
            else
            {
                throw new NotSupportedException($"Unknown compression type: {compressionType}");
            }
        }

        private byte[] ReadBytes(long position, long length)
        {
            var result = new byte[length];
            _stream.Seek(position, SeekOrigin.Begin);
            _br.Read(result, 0, (int)length);
            return result;
        }

        private static Header ReadHeader(BinaryReader br)
        {
            return new Header()
            {
                wMagic = br.ReadUInt32(),
                MajorVersion = br.ReadByte(),
                MinorVersion = br.ReadByte(),
                Feature = br.ReadInt16(),
                TotalFiles = br.ReadInt32(),
                Hash = br.ReadUInt32()
            };
        }

        private static bool SupportsVersion(int major, int minor)
        {
            if (major == 2 && minor == 0)
                return true;
            if (major == 4 && minor == 0)
                return true;
            if (major == 4 && minor == 1)
                return true;
            return false;
        }

        private static ImmutableArray<Entry> ReadEntries(in Header header, BinaryReader br)
        {
            var entrySize = header.MajorVersion <= 2 ? 24 : 48;
            var tableData = br.ReadBytes(header.TotalFiles * entrySize);
            if (header.Feature == 8)
            {
                var key = br.ReadBytes(128);
                tableData = PakCipher.DecryptData(tableData, key);
            }

            var br2 = new BinaryReader(new MemoryStream(tableData));
            var entries = new Entry[header.TotalFiles];
            if (entrySize == 24)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    ref var entry = ref entries[i];
                    entry.Offset = br2.ReadInt64();
                    entry.DecompressedSize = br2.ReadInt64();
                    entry.HashNameLower = br2.ReadUInt32();
                    entry.HashNameUpper = br2.ReadUInt32();
                }
            }
            else
            {

                for (var i = 0; i < entries.Length; i++)
                {
                    ref var entry = ref entries[i];
                    entry.HashNameLower = br2.ReadUInt32();
                    entry.HashNameUpper = br2.ReadUInt32();
                    entry.Offset = br2.ReadInt64();
                    entry.CompressedSize = br2.ReadInt64();
                    entry.DecompressedSize = br2.ReadInt64();
                    entry.CompressionType = br2.ReadByte();
                    entry.CompressionFlags = br2.ReadByte();
                    entry.EncryptionType = br2.ReadByte();
                    entry.EncryptionFlags = br2.ReadByte();
                    entry.Reserved = br2.ReadInt32();
                    entry.Checksum = br2.ReadUInt64();
                }
            }
            return [.. entries];
        }

        internal static ulong GetNormalizedPathHash(string path)
        {
            path = path.Replace("\\", "/");
            if (path.Contains("__Unknown"))
            {
                var pathWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(path);
                return Convert.ToUInt64(pathWithoutExtension, 16);
            }
            else
            {
                var lower = Hash32(path.ToLowerInvariant());
                var upper = Hash32(path.ToUpperInvariant());
                return ((ulong)upper << 32) | lower;
            }
        }

        internal static uint Hash32(string path) => (uint)MurMur3.HashData(path);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Header
        {
            public uint wMagic; //0x414B504B (KPKA)
            public byte MajorVersion; // 2 (Kitchen Demo PS4), 4
            public byte MinorVersion; // 0
            public short Feature; // 0, 8 (Encrypted -> Monster Hunter Rise & Monster Hunter Rise - Sunbreak Demo)
            public int TotalFiles;
            public uint Hash;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Entry
        {
            public uint HashNameLower;
            public uint HashNameUpper;
            public long Offset;
            public long CompressedSize;
            public long DecompressedSize;
            public byte CompressionType;
            public byte CompressionFlags;
            public byte EncryptionType;
            public byte EncryptionFlags;
            public int Reserved;
            public ulong Checksum;

            public ulong HashName
            {
                readonly get => ((ulong)HashNameUpper << 32) | HashNameLower;
                set
                {
                    HashNameLower = (uint)(value & 0xFFFFFFFF);
                    HashNameUpper = (uint)(value >> 32);
                }
            }
        }
    }
}
