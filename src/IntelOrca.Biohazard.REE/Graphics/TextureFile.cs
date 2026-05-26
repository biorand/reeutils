using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Graphics
{
    public sealed class TextureFile
    {
        private const uint MAGIC = 0x00584554; // "TEX\0"

        private readonly TextureHeaderCommon _header;
        private readonly TextureHeaderV1? _headerV1;
        private readonly TextureHeaderV2? _headerV2;
        private readonly ImmutableArray<TextureMip> _mips;

        public TextureFile(ReadOnlyMemory<byte> data)
        {
            if (data.Length < Marshal.SizeOf<TextureHeaderCommon>())
                throw new InvalidDataException("TEX data is smaller than the fixed header.");

            Data = data;
            _header = ReadStruct<TextureHeaderCommon>(0);
            if (_header.Magic != MAGIC)
                throw new InvalidDataException($"Invalid TEX magic: 0x{_header.Magic:X8}");

            if (EffectiveVersion > 27)
            {
                if (data.Length < HeaderV2Offset + Marshal.SizeOf<TextureHeaderV2>() + 8)
                    throw new InvalidDataException("TEX data is smaller than the version 2 header.");

                _headerV2 = ReadStruct<TextureHeaderV2>(HeaderV2Offset);
                _mips = ReadMips(HeaderV2MipOffset, _headerV2.Value.NumImages, _headerV2.Value.OneImgMipHdrSize / 16);
            }
            else
            {
                if (data.Length < HeaderV1Offset + Marshal.SizeOf<TextureHeaderV1>())
                    throw new InvalidDataException("TEX data is smaller than the version 1 header.");

                _headerV1 = ReadStruct<TextureHeaderV1>(HeaderV1Offset);
                _mips = ReadMips(HeaderV1MipOffset, _headerV1.Value.NumImages, _headerV1.Value.MipCount);
            }
        }

        public ReadOnlyMemory<byte> Data { get; }

        public uint RawVersion => _header.Version;

        public int EffectiveVersion => NormalizeVersion((int)_header.Version);

        public ushort Width => _header.Width;

        public ushort Height => _header.Height;

        public ushort Unknown00 => _header.Unk00;

        public int Size => Data.Length;

        public uint FormatId => _headerV2?.Format ?? _headerV1?.Format ?? 0;

        public TextureCompression Compression => Enum.IsDefined(typeof(TextureCompression), FormatId)
            ? (TextureCompression)FormatId
            : TextureCompression.Unknown;

        public int ImageCount => _headerV2?.NumImages ?? _headerV1?.NumImages ?? 0;

        public int MipCount => _headerV2.HasValue
            ? _headerV2.Value.OneImgMipHdrSize / 16
            : _headerV1?.MipCount ?? 0;

        public int TotalMipCount => _mips.Length;

        public ImmutableArray<TextureMip> Mips => _mips;

        public TextureMip GetMip(int imageIndex = 0, int mipIndex = 0)
        {
            var index = GetMipIndex(imageIndex, mipIndex);
            return _mips[index];
        }

        public ReadOnlyMemory<byte> GetMipData(int imageIndex = 0, int mipIndex = 0)
        {
            var mip = GetMip(imageIndex, mipIndex);
            return Data.Slice((int)mip.Offset, (int)mip.Size);
        }

        public byte[] ToDdsBytes()
        {
            return DdsFile.FromTexture(this).ToBytes();
        }

        public TextureHeaderCommon Header => _header;

        public TextureHeaderV1? HeaderV1 => _headerV1;

        public TextureHeaderV2? HeaderV2 => _headerV2;

        private static int NormalizeVersion(int version)
        {
            if (version == 190820018)
                return 10;
            if (version == 143221013)
                return 36;
            return version;
        }

        private int GetMipIndex(int imageIndex, int mipIndex)
        {
            if (imageIndex < 0 || imageIndex >= ImageCount)
                throw new ArgumentOutOfRangeException(nameof(imageIndex));
            if (mipIndex < 0 || mipIndex >= MipCount)
                throw new ArgumentOutOfRangeException(nameof(mipIndex));

            var index = imageIndex * MipCount + mipIndex;
            if (index < 0 || index >= _mips.Length)
                throw new ArgumentOutOfRangeException();
            return index;
        }

        private ImmutableArray<TextureMip> ReadMips(int mipOffset, int imageCount, int mipCount)
        {
            if (imageCount < 0 || mipCount < 0)
                throw new InvalidDataException("Invalid TEX mip counts.");

            var result = ImmutableArray.CreateBuilder<TextureMip>(imageCount * mipCount);
            var offset = mipOffset;
            for (var i = 0; i < imageCount; i++)
            {
                for (var j = 0; j < mipCount; j++)
                {
                    if (offset + Marshal.SizeOf<TextureMip>() > Data.Length)
                        throw new InvalidDataException("TEX mip table exceeds the file size.");

                    var mip = ReadStruct<TextureMip>(offset);
                    result.Add(mip);
                    offset += Marshal.SizeOf<TextureMip>();
                }
            }

            return result.ToImmutable();
        }

        private T ReadStruct<T>(int offset) where T : unmanaged
        {
            return MemoryMarshal.Read<T>(Data.Span.Slice(offset));
        }

        private static int HeaderV1Offset => Marshal.SizeOf<TextureHeaderCommon>();
        private static int HeaderV2Offset => Marshal.SizeOf<TextureHeaderCommon>();
        private static int HeaderV1MipOffset => HeaderV1Offset + Marshal.SizeOf<TextureHeaderV1>();
        private static int HeaderV2MipOffset => HeaderV2Offset + Marshal.SizeOf<TextureHeaderV2>() + 8;
    }

    public enum TextureCompression : uint
    {
        Unknown = 0,
        R8G8B8A8Unorm = 28,
        R8G8B8A8UnormSrgb = 29,
        Bc1Unorm = 71,
        Bc1UnormSrgb = 72,
        Bc2Unorm = 74,
        Bc2UnormSrgb = 75,
        Bc3Unorm = 77,
        Bc3UnormSrgb = 78,
        Bc4Unorm = 80,
        Bc5Unorm = 83,
        Bc6HUf16 = 95,
        Bc6HSf16 = 96,
        Bc7Unorm = 98,
        Bc7UnormSrgb = 99,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureHeaderCommon
    {
        public uint Magic;
        public uint Version;
        public ushort Width;
        public ushort Height;
        public ushort Unk00;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureHeaderV1
    {
        public byte MipCount;
        public byte NumImages;
        public uint Format;
        public uint Unk02;
        public uint Unk03;
        public uint Unk04;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureHeaderV2
    {
        public byte NumImages;
        public byte OneImgMipHdrSize;
        public uint Format;
        public uint Unk02;
        public uint Unk03;
        public uint Unk04;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureMip
    {
        public ulong Offset;
        public uint Pitch;
        public uint Size;
    }
}
