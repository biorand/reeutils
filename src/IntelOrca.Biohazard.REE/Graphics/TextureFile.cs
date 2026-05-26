using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Graphics
{
    public sealed class TextureFile
    {
        private const uint MAGIC = 0x00584554; // "TEX\0"
        private const ushort GDeflateMagic = 0xFB04;

        private readonly TextureHeaderCommon _header;
        private readonly TextureHeaderV1? _headerV1;
        private readonly TextureHeaderV2? _headerV2;
        private readonly ImmutableArray<TextureMip> _mips;
        private readonly ImmutableArray<TexturePackedMip> _packedMips;
        private readonly int _packedPayloadOffset;

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

            _packedMips = TryReadPackedMips();
            _packedPayloadOffset = _packedMips.IsDefaultOrEmpty || _mips.IsDefaultOrEmpty
                ? 0
                : checked((int)_mips[0].Offset + (_packedMips.Length * Marshal.SizeOf<TexturePackedMip>()));
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

        public bool UsesPackedMips => !_packedMips.IsDefaultOrEmpty && _packedMips.Length != 0;

        public TextureMip GetMip(int imageIndex = 0, int mipIndex = 0)
        {
            var index = GetMipIndex(imageIndex, mipIndex);
            return _mips[index];
        }

        public ReadOnlyMemory<byte> GetMipData(int imageIndex = 0, int mipIndex = 0, TextureConvertOptions? options = null)
        {
            var index = GetMipIndex(imageIndex, mipIndex);
            var mip = _mips[index];
            var mipData = ReadMipBytes(index, mip, options);
            if (!DdsFile.TryGetFormatInfo(FormatId, out var formatInfo))
                return mipData;

            var mipWidth = Math.Max(1, Width >> mipIndex);
            var mipHeight = Math.Max(1, Height >> mipIndex);
            var expectedPitch = DdsFile.GetMipPitch(mipWidth, mipHeight, formatInfo);
            var expectedSize = DdsFile.GetMipSize(mipWidth, mipHeight, formatInfo);

            if (mip.Pitch > expectedPitch)
                return StripPitch(mipData.Span, mipHeight, (int)mip.Pitch, expectedPitch, formatInfo.IsBlockCompressed);

            return mipData.Slice(0, Math.Min(mipData.Length, Math.Min((int)mip.Size, expectedSize)));
        }

        public DdsFile ToDds(TextureConvertOptions? options = null)
        {
            return DdsFile.FromTexture(this, options);
        }

        public TextureHeaderCommon Header => _header;

        public TextureHeaderV1? HeaderV1 => _headerV1;

        public TextureHeaderV2? HeaderV2 => _headerV2;

        internal static int NormalizeVersion(int version)
        {
            if (version == 190820018)
                return 10;
            if (version == 143221013)
                return 36;
            return version;
        }

        internal static uint EncodeVersion(int version)
        {
            return version switch
            {
                36 => 143221013u,
                _ => (uint)version,
            };
        }

        internal static int GetTextureFlags(int effectiveVersion, DdsFile.TextureFormatInfo formatInfo)
        {
            if (effectiveVersion <= 20)
                return 0x0001;

            return formatInfo.IsBlockCompressed ? 0x0580 : 0x0080;
        }

        internal static bool IsGDeflatePayload(ReadOnlySpan<byte> payload)
        {
            return payload.Length >= 2
                && payload[0] == 0x04
                && payload[1] == 0xFB;
        }

        internal static bool UsesPackedMipHeaders(uint rawVersion)
        {
            return rawVersion is 241106027u or 250813143u or 251111100u;
        }

        private ReadOnlyMemory<byte> ReadMipBytes(int index, TextureMip mip, TextureConvertOptions? options)
        {
            if (!UsesPackedMips)
            {
                if (mip.Offset > (ulong)Data.Length || mip.Size > int.MaxValue)
                    throw new InvalidDataException("TEX mip data exceeds the file size.");

                var mipStart = (int)mip.Offset;
                var mipSize = (int)mip.Size;
                if (mipSize > Data.Length - mipStart)
                    throw new InvalidDataException("TEX mip data exceeds the file size.");

                return Data.Slice(mipStart, mipSize);
            }

            var packedMip = _packedMips[index];
            var start = checked(_packedPayloadOffset + (int)packedMip.Offset);
            var size = checked((int)packedMip.Size);
            if (start < 0 || size < 0 || start > Data.Length || size > Data.Length - start)
                throw new InvalidDataException("TEX packed mip data exceeds the file size.");

            var result = Data.Slice(start, size);
            if (result.Length >= 2 && BinaryPrimitives.ReadUInt16LittleEndian(result.Span) == GDeflateMagic)
            {
                var encoder = options?.Encoder;
                if (encoder is null)
                    throw new NotSupportedException("TEX packed mip data is gdeflate-compressed. Provide an IGDeflateEncoder inside TextureConvertOptions.");

                var decoded = encoder.Decompress(result, checked((int)mip.Size));
                if (decoded.Length < mip.Size)
                    throw new InvalidDataException("GDeflate encoder returned less data than the mip requires.");

                return decoded.AsMemory(0, checked((int)mip.Size));
            }

            return result;
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

        private ImmutableArray<TexturePackedMip> TryReadPackedMips()
        {
            if (!UsesPackedMipHeaders(RawVersion) || _mips.IsDefaultOrEmpty || _mips.Length == 0)
                return [];

            var headerOffset = checked((int)_mips[0].Offset);
            var headerSize = checked(_mips.Length * Marshal.SizeOf<TexturePackedMip>());
            if (headerOffset < 0 || headerOffset > Data.Length || headerSize > Data.Length - headerOffset)
                return [];

            var result = ImmutableArray.CreateBuilder<TexturePackedMip>(_mips.Length);
            var offset = headerOffset;
            for (var i = 0; i < _mips.Length; i++)
            {
                result.Add(ReadStruct<TexturePackedMip>(offset));
                offset += Marshal.SizeOf<TexturePackedMip>();
            }

            var packedPayloadOffset = headerOffset + headerSize;
            var maxPayloadLength = Data.Length - packedPayloadOffset;
            var previousOffset = -1;
            for (var i = 0; i < result.Count; i++)
            {
                var packedMip = result[i];
                if (packedMip.Size > int.MaxValue || packedMip.Offset > int.MaxValue)
                    return [];

                var packedSize = (int)packedMip.Size;
                var packedOffset = (int)packedMip.Offset;
                if (packedSize < 0 || packedOffset < 0)
                    return [];
                if (i == 0 && packedOffset != 0)
                    return [];
                if (packedOffset < previousOffset)
                    return [];
                if (packedOffset + packedSize > maxPayloadLength)
                    return [];

                previousOffset = packedOffset;
            }

            return result.ToImmutable();
        }

        private static ReadOnlyMemory<byte> StripPitch(ReadOnlySpan<byte> source, int height, int sourcePitch, int expectedPitch, bool blockCompressed)
        {
            var rowCount = blockCompressed
                ? Math.Max(1, (height + 3) / 4)
                : height;
            var result = new byte[checked(rowCount * expectedPitch)];
            var sourceOffset = 0;
            var destinationOffset = 0;
            for (var i = 0; i < rowCount; i++)
            {
                if (sourceOffset + expectedPitch > source.Length)
                    throw new InvalidDataException("TEX mip pitch exceeds the mip data size.");

                source.Slice(sourceOffset, expectedPitch).CopyTo(result.AsSpan(destinationOffset, expectedPitch));
                sourceOffset += sourcePitch;
                destinationOffset += expectedPitch;
            }

            return result;
        }

        private T ReadStruct<T>(int offset) where T : unmanaged
        {
            return MemoryMarshal.Read<T>(Data.Span.Slice(offset));
        }

        private static int HeaderV1Offset => Marshal.SizeOf<TextureHeaderCommon>();
        private static int HeaderV2Offset => Marshal.SizeOf<TextureHeaderCommon>();
        private static int HeaderV1MipOffset => HeaderV1Offset + Marshal.SizeOf<TextureHeaderV1>();
        private static int HeaderV2MipOffset => HeaderV2Offset + Marshal.SizeOf<TextureHeaderV2>() + 8;

        public class Builder
        {
            public int Version { get; set; }
            public ushort Width { get; set; }
            public ushort Height { get; set; }
            public int ImageCount { get; set; }
            public int MipCount { get; set; }
            public uint FormatId { get; set; }
            public List<byte[]> MipData { get; set; } = new();

            public TextureFile Build(TextureConvertOptions? options = null)
            {
                if (!DdsFile.TryGetFormatInfo(FormatId, out var formatInfo))
                    throw new InvalidDataException($"Unsupported TEX format: {FormatId}.");

                if (MipData.Count != checked(ImageCount * MipCount))
                    throw new InvalidDataException("Mip data does not match the image/mip counts.");

                var effectiveVersion = NormalizeVersion(Version);
                var rawVersion = EncodeVersion(Version);

                var ms = new MemoryStream();
                using (var writer = new BinaryWriter(ms))
                {
                    if (UsesPackedMipHeaders(rawVersion))
                    {
                        var encoder = options?.Encoder;
                        if (encoder is null)
                            throw new NotSupportedException($"Building TEX version {rawVersion} requires an IGDeflateEncoder inside TextureConvertOptions.");

                        var fixedHeaderLength = 40;
                        var mipTableLength = MipData.Count * 16;
                        var packedHeaderOffset = fixedHeaderLength + mipTableLength;
                        var packedHeaderLength = MipData.Count * 8;
                        var packedPayloads = new byte[MipData.Count][];

                        for (var i = 0; i < MipData.Count; i++)
                        {
                            var mipBytes = MipData[i];
                            var compressed = encoder.Compress(mipBytes);
                            packedPayloads[i] = compressed.Length > 0 && compressed.Length < mipBytes.Length && IsGDeflatePayload(compressed)
                                ? compressed
                                : mipBytes;
                        }

                        writer.Write(MAGIC);
                        writer.Write(rawVersion);
                        writer.Write(Width);
                        writer.Write(Height);
                        writer.Write((ushort)0);
                        writer.Write((byte)ImageCount);
                        writer.Write((byte)(MipCount * 16));
                        writer.Write(FormatId);
                        writer.Write(0xFFFFFFFFu);
                        writer.Write(0u);
                        writer.Write((uint)GetTextureFlags(effectiveVersion, formatInfo));
                        writer.Write(new byte[8]);

                        var currentMipOffset = packedHeaderOffset;
                        var mipIndex = 0;
                        for (var imageIndex = 0; imageIndex < ImageCount; imageIndex++)
                        {
                            var mipWidth = (int)Width;
                            var mipHeight = (int)Height;
                            for (var level = 0; level < MipCount; level++)
                            {
                                var mipBytes = MipData[mipIndex++];
                                writer.Write((ulong)currentMipOffset);
                                writer.Write((uint)DdsFile.GetMipPitch(mipWidth, mipHeight, formatInfo));
                                writer.Write((uint)mipBytes.Length);
                                currentMipOffset += mipBytes.Length;
                                mipWidth = Math.Max(1, mipWidth / 2);
                                mipHeight = Math.Max(1, mipHeight / 2);
                            }
                        }

                        var currentPayloadOffset = 0;
                        foreach (var payload in packedPayloads)
                        {
                            writer.Write((uint)payload.Length);
                            writer.Write((uint)currentPayloadOffset);
                            currentPayloadOffset += payload.Length;
                        }

                        foreach (var payload in packedPayloads)
                            writer.Write(payload);
                    }
                    else
                    {
                        var fixedHeaderLength = effectiveVersion > 27 ? 40 : 32;
                        var dataOffset = fixedHeaderLength + (MipData.Count * 16);

                        writer.Write(MAGIC);
                        writer.Write(rawVersion);
                        writer.Write(Width);
                        writer.Write(Height);
                        writer.Write((ushort)0);

                        if (effectiveVersion > 27)
                        {
                            writer.Write((byte)ImageCount);
                            writer.Write((byte)(MipCount * 16));
                            writer.Write(FormatId);
                            writer.Write(0xFFFFFFFFu);
                            writer.Write(0u);
                            writer.Write((uint)GetTextureFlags(effectiveVersion, formatInfo));
                            writer.Write(new byte[8]);
                        }
                        else
                        {
                            writer.Write((byte)MipCount);
                            writer.Write((byte)ImageCount);
                            writer.Write(FormatId);
                            writer.Write(0xFFFFFFFFu);
                            writer.Write(0u);
                            writer.Write(0u);
                        }

                        var currentOffset = dataOffset;
                        var mipIndex = 0;
                        for (var imageIndex = 0; imageIndex < ImageCount; imageIndex++)
                        {
                            var mipWidth = (int)Width;
                            var mipHeight = (int)Height;
                            for (var level = 0; level < MipCount; level++)
                            {
                                var mipBytes = MipData[mipIndex++];
                                writer.Write((ulong)currentOffset);
                                writer.Write((uint)DdsFile.GetMipPitch(mipWidth, mipHeight, formatInfo));
                                writer.Write((uint)mipBytes.Length);
                                currentOffset += mipBytes.Length;
                                mipWidth = Math.Max(1, mipWidth / 2);
                                mipHeight = Math.Max(1, mipHeight / 2);
                            }
                        }

                        foreach (var mip in MipData)
                            writer.Write(mip);
                    }
                }

                return new TextureFile(ms.ToArray());
            }
        }
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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TexturePackedMip
    {
        public uint Size;
        public uint Offset;
    }
}
