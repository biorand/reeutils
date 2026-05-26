using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Graphics;

public sealed class DdsFile
{
    private const uint Magic = 0x20534444;
    private const uint TrailerMagic = 0x30524545;
    private const int HeaderSize = 124;
    private const int PixelFormatSize = 32;
    private const uint FourCcDx10 = 0x30315844;
    private const uint DdsdCaps = 0x00000001;
    private const uint DdsdHeight = 0x00000002;
    private const uint DdsdWidth = 0x00000004;
    private const uint DdsdPitch = 0x00000008;
    private const uint DdsdPixelFormat = 0x00001000;
    private const uint DdsdMipmapCount = 0x00020000;
    private const uint DdsdLinearSize = 0x00080000;
    private const uint DdpfAlphaPixels = 0x00000001;
    private const uint DdpfFourCc = 0x00000004;
    private const uint DdpfRgb = 0x00000040;
    private const uint DdsCapsTexture = 0x00001000;
    private const uint DdsCapsComplex = 0x00000008;
    private const uint DdsCapsMipmap = 0x00400000;
    private const int D3d10ResourceDimensionTexture2D = 3;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public uint FormatId { get; private set; }
    public int ImageCount { get; private set; } = 1;
    public int MipCount { get; private set; } = 1;
    public byte[][] MipData { get; private set; } = [];
    public byte[]? OriginalTexBytes { get; private set; }

    public static DdsFile FromTexture(TextureFile texture)
    {
        var mipData = new byte[texture.TotalMipCount][];
        var index = 0;
        for (var imageIndex = 0; imageIndex < texture.ImageCount; imageIndex++)
        {
            for (var mipIndex = 0; mipIndex < texture.MipCount; mipIndex++)
            {
                mipData[index++] = texture.GetMipData(imageIndex, mipIndex).ToArray();
            }
        }

        return new DdsFile
        {
            Width = texture.Width,
            Height = texture.Height,
            FormatId = texture.FormatId,
            ImageCount = texture.ImageCount,
            MipCount = texture.MipCount,
            MipData = mipData,
            OriginalTexBytes = texture.Data.ToArray(),
        };
    }

    public static DdsFile Read(byte[] data)
    {
        var reader = new SpanReader(data);
        if (reader.ReadUInt32() != Magic)
            throw new InvalidDataException("Not a DDS file.");

        if (reader.ReadInt32() != HeaderSize)
            throw new InvalidDataException("Unsupported DDS header size.");

        var flags = reader.ReadUInt32();
        var height = reader.ReadInt32();
        var width = reader.ReadInt32();
        _ = reader.ReadInt32(); // pitchOrLinearSize
        _ = reader.ReadInt32(); // depth
        var mipCount = reader.ReadInt32();
        reader.Advance(44);

        if (reader.ReadInt32() != PixelFormatSize)
            throw new InvalidDataException("Unsupported DDS pixel format.");

        var pixelFormatFlags = reader.ReadUInt32();
        var fourCc = reader.ReadUInt32();
        var rgbBitCount = reader.ReadInt32();
        var rMask = reader.ReadUInt32();
        var gMask = reader.ReadUInt32();
        var bMask = reader.ReadUInt32();
        var aMask = reader.ReadUInt32();
        var caps = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // caps2
        _ = reader.ReadUInt32(); // caps3
        _ = reader.ReadUInt32(); // caps4
        _ = reader.ReadUInt32(); // reserved2

        var hasMipMaps = (flags & DdsdMipmapCount) != 0 && mipCount > 0;
        mipCount = hasMipMaps ? mipCount : 1;

        uint formatId;
        var imageCount = 1;

        if ((pixelFormatFlags & DdpfFourCc) != 0)
        {
            if (fourCc == FourCcDx10)
            {
                formatId = reader.ReadUInt32();
                var resourceDimension = reader.ReadUInt32();
                _ = reader.ReadUInt32(); // miscFlag
                imageCount = reader.ReadInt32();
                _ = reader.ReadUInt32(); // miscFlags2

                if (resourceDimension != D3d10ResourceDimensionTexture2D)
                    throw new InvalidDataException("Only DDS texture2D resources are supported.");
            }
            else
            {
                formatId = MapLegacyFourCcToDxgi(fourCc);
            }
        }
        else if ((pixelFormatFlags & DdpfRgb) != 0 && rgbBitCount == 32)
        {
            if (rMask == 0x00FF0000 && gMask == 0x0000FF00 && bMask == 0x000000FF && aMask == 0xFF000000)
            {
                formatId = 28;
            }
            else
            {
                throw new InvalidDataException("Unsupported DDS RGB mask layout.");
            }
        }
        else
        {
            throw new InvalidDataException("Unsupported DDS pixel format.");
        }

        if (!TryGetFormatInfo(formatId, out var formatInfo))
            throw new InvalidDataException($"Unsupported DDS format: {formatId}.");

        if (imageCount <= 0)
            throw new InvalidDataException("DDS array size must be positive.");

        var mipData = new byte[checked(imageCount * mipCount)][];
        var index = 0;
        for (var imageIndex = 0; imageIndex < imageCount; imageIndex++)
        {
            var mipWidth = width;
            var mipHeight = height;
            for (var mipIndex = 0; mipIndex < mipCount; mipIndex++)
            {
                var size = GetMipSize(mipWidth, mipHeight, formatInfo);
                if (reader.Remaining < size)
                    throw new InvalidDataException("DDS pixel data is truncated.");

                mipData[index++] = reader.ReadBytes(size);
                mipWidth = Math.Max(1, mipWidth / 2);
                mipHeight = Math.Max(1, mipHeight / 2);
            }
        }

        byte[]? originalTexBytes = null;
        if (reader.Remaining >= 8)
        {
            var trailerMagic = reader.ReadUInt32();
            if (trailerMagic == TrailerMagic)
            {
                var texLength = reader.ReadInt32();
                if (texLength < 0 || reader.Remaining < texLength)
                    throw new InvalidDataException("DDS trailer is truncated.");

                originalTexBytes = reader.ReadBytes(texLength);
            }
        }

        return new DdsFile
        {
            Width = width,
            Height = height,
            FormatId = formatId,
            ImageCount = imageCount,
            MipCount = mipCount,
            MipData = mipData,
            OriginalTexBytes = originalTexBytes,
        };
    }

    public byte[] ToBytes()
    {
        if (!TryGetFormatInfo(FormatId, out var formatInfo))
            throw new InvalidDataException($"Unsupported DDS format: {FormatId}.");

        if (MipData.Length != checked(ImageCount * MipCount))
            throw new InvalidDataException("Mip data does not match the image/mip counts.");

        var usesDx10 = formatInfo.LegacyFourCc == 0;
        var headerLength = 128 + (usesDx10 ? 20 : 0);
        var payloadLength = MipData.Sum(x => x.Length);
        var trailerLength = OriginalTexBytes?.Length is int texLength ? 8 + texLength : 0;
        var data = new byte[checked(headerLength + payloadLength + trailerLength)];
        var writer = new SpanWriter(data);

        var flags = DdsdCaps | DdsdHeight | DdsdWidth | DdsdPixelFormat;
        flags |= formatInfo.IsBlockCompressed ? DdsdLinearSize : DdsdPitch;
        if (MipCount > 1)
            flags |= DdsdMipmapCount;

        writer.WriteUInt32(Magic);
        writer.WriteInt32(HeaderSize);
        writer.WriteUInt32(flags);
        writer.WriteInt32(Height);
        writer.WriteInt32(Width);
        writer.WriteInt32(GetMipSize(Width, Height, formatInfo));
        writer.WriteInt32(0);
        writer.WriteInt32(MipCount);
        writer.WriteZeros(44);

        writer.WriteInt32(PixelFormatSize);
        if (usesDx10)
        {
            writer.WriteUInt32(DdpfFourCc);
            writer.WriteUInt32(FourCcDx10);
            writer.WriteInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
        }
        else if (formatInfo.LegacyFourCc != 0)
        {
            writer.WriteUInt32(DdpfFourCc);
            writer.WriteUInt32(formatInfo.LegacyFourCc);
            writer.WriteInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
        }
        else
        {
            writer.WriteUInt32(DdpfRgb | DdpfAlphaPixels);
            writer.WriteUInt32(0);
            writer.WriteInt32(32);
            writer.WriteUInt32(0x00FF0000);
            writer.WriteUInt32(0x0000FF00);
            writer.WriteUInt32(0x000000FF);
            writer.WriteUInt32(0xFF000000);
        }

        var caps = DdsCapsTexture;
        if (MipCount > 1)
            caps |= DdsCapsComplex | DdsCapsMipmap;

        writer.WriteUInt32(caps);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);

        if (usesDx10)
        {
            writer.WriteUInt32(FormatId);
            writer.WriteUInt32(D3d10ResourceDimensionTexture2D);
            writer.WriteUInt32(0);
            writer.WriteInt32(ImageCount);
            writer.WriteUInt32(0);
        }

        foreach (var mip in MipData)
            writer.WriteBytes(mip);

        if (OriginalTexBytes is { Length: > 0 } texBytes)
        {
            writer.WriteUInt32(TrailerMagic);
            writer.WriteInt32(texBytes.Length);
            writer.WriteBytes(texBytes);
        }

        return data;
    }

    public byte[] ToTextureBytes(int version)
    {
        if (OriginalTexBytes is not null)
            return OriginalTexBytes;

        if (!TryGetFormatInfo(FormatId, out var formatInfo))
            throw new InvalidDataException($"Unsupported TEX format: {FormatId}.");

        if (MipData.Length != checked(ImageCount * MipCount))
            throw new InvalidDataException("Mip data does not match the image/mip counts.");

        var effectiveVersion = NormalizeVersion(version);
        var rawVersion = EncodeVersion(version);
        var fixedHeaderLength = effectiveVersion > 27 ? 40 : 32;
        var dataOffset = fixedHeaderLength + (MipData.Length * 16);
        var totalLength = checked(dataOffset + MipData.Sum(x => x.Length));
        var data = new byte[totalLength];
        var writer = new SpanWriter(data);

        writer.WriteUInt32(0x00584554);
        writer.WriteUInt32(rawVersion);
        writer.WriteUInt16((ushort)Width);
        writer.WriteUInt16((ushort)Height);
        writer.WriteUInt16(0);

        if (effectiveVersion > 27)
        {
            writer.WriteByte((byte)ImageCount);
            writer.WriteByte((byte)(MipCount * 16));
            writer.WriteUInt32(FormatId);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteZeros(8);
        }
        else
        {
            writer.WriteByte((byte)MipCount);
            writer.WriteByte((byte)ImageCount);
            writer.WriteUInt32(FormatId);
            writer.WriteUInt32(0xFFFFFFFF);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
        }

        var currentOffset = dataOffset;
        var mipIndex = 0;
        for (var imageIndex = 0; imageIndex < ImageCount; imageIndex++)
        {
            var mipWidth = Width;
            var mipHeight = Height;
            for (var level = 0; level < MipCount; level++)
            {
                var mipBytes = MipData[mipIndex++];
                writer.WriteUInt64((ulong)currentOffset);
                writer.WriteUInt32((uint)GetMipPitch(mipWidth, mipHeight, formatInfo));
                writer.WriteUInt32((uint)mipBytes.Length);
                mipBytes.CopyTo(data, currentOffset);
                currentOffset += mipBytes.Length;
                mipWidth = Math.Max(1, mipWidth / 2);
                mipHeight = Math.Max(1, mipHeight / 2);
            }
        }

        return data;
    }

    private static uint EncodeVersion(int version)
    {
        return version switch
        {
            36 => 143221013u,
            _ => (uint)version,
        };
    }

    private static int NormalizeVersion(int version)
    {
        return version switch
        {
            190820018 => 10,
            143221013 => 36,
            _ => version,
        };
    }

    private static uint MapLegacyFourCcToDxgi(uint fourCc)
    {
        return fourCc switch
        {
            0x31545844 => 71, // DXT1
            0x33545844 => 74, // DXT3
            0x35545844 => 77, // DXT5
            0x31495441 => 80, // ATI1
            0x32495441 => 83, // ATI2
            _ => throw new InvalidDataException($"Unsupported DDS FourCC: 0x{fourCc:X8}.")
        };
    }

    private static int GetMipPitch(int width, int height, TextureFormatInfo formatInfo)
    {
        if (formatInfo.IsBlockCompressed)
        {
            var blocksWide = Math.Max(1, (width + 3) / 4);
            return blocksWide * formatInfo.UnitSize;
        }

        return width * formatInfo.UnitSize;
    }

    private static int GetMipSize(int width, int height, TextureFormatInfo formatInfo)
    {
        if (formatInfo.IsBlockCompressed)
        {
            var blocksWide = Math.Max(1, (width + 3) / 4);
            var blocksHigh = Math.Max(1, (height + 3) / 4);
            return blocksWide * blocksHigh * formatInfo.UnitSize;
        }

        return width * height * formatInfo.UnitSize;
    }

    private static bool TryGetFormatInfo(uint formatId, out TextureFormatInfo formatInfo)
    {
        formatInfo = formatId switch
        {
            28 or 29 => new TextureFormatInfo(false, 4, 0),
            71 or 72 => new TextureFormatInfo(true, 8, 0x31545844),
            74 or 75 => new TextureFormatInfo(true, 16, 0x33545844),
            77 or 78 => new TextureFormatInfo(true, 16, 0x35545844),
            80 => new TextureFormatInfo(true, 8, 0x31495441),
            83 => new TextureFormatInfo(true, 16, 0x32495441),
            95 or 96 or 98 or 99 => new TextureFormatInfo(true, 16, 0),
            _ => default,
        };

        return formatInfo.UnitSize != 0;
    }

    private readonly record struct TextureFormatInfo(bool IsBlockCompressed, int UnitSize, uint LegacyFourCc);

    private sealed class SpanReader(byte[] data)
    {
        private readonly byte[] _data = data;
        private int _offset;

        public int Remaining => _data.Length - _offset;

        public uint ReadUInt32()
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_offset, 4));
            _offset += 4;
            return value;
        }

        public int ReadInt32()
        {
            var value = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_offset, 4));
            _offset += 4;
            return value;
        }

        public byte[] ReadBytes(int length)
        {
            var value = _data.AsSpan(_offset, length).ToArray();
            _offset += length;
            return value;
        }

        public void Advance(int length) => _offset += length;
    }

    private sealed class SpanWriter(byte[] data)
    {
        private readonly byte[] _data = data;
        private int _offset;

        public void WriteUInt32(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(_offset, 4), value);
            _offset += 4;
        }

        public void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(_data.AsSpan(_offset, 8), value);
            _offset += 8;
        }

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_data.AsSpan(_offset, 4), value);
            _offset += 4;
        }

        public void WriteUInt16(ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(_offset, 2), value);
            _offset += 2;
        }

        public void WriteByte(byte value)
        {
            _data[_offset++] = value;
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            value.CopyTo(_data.AsSpan(_offset, value.Length));
            _offset += value.Length;
        }

        public void WriteZeros(int length)
        {
            _data.AsSpan(_offset, length).Clear();
            _offset += length;
        }
    }
}
