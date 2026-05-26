using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Graphics;

public sealed class DdsFile
{
    private const uint Magic = 0x20534444;
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

    public ReadOnlyMemory<byte> Data { get; }

    public int Width => BinaryPrimitives.ReadInt32LittleEndian(Data.Span.Slice(16, 4));
    public int Height => BinaryPrimitives.ReadInt32LittleEndian(Data.Span.Slice(12, 4));

    public uint FormatId
    {
        get
        {
            var pixelFormatFlags = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(80, 4));
            if ((pixelFormatFlags & DdpfFourCc) != 0)
            {
                var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(84, 4));
                if (fourCc == FourCcDx10)
                {
                    return BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(128, 4));
                }
                else
                {
                    return MapLegacyFourCcToDxgi(fourCc);
                }
            }
            else if ((pixelFormatFlags & DdpfRgb) != 0 && BinaryPrimitives.ReadInt32LittleEndian(Data.Span.Slice(88, 4)) == 32)
            {
                var rMask = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(92, 4));
                var gMask = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(96, 4));
                var bMask = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(100, 4));
                var aMask = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(104, 4));
                if (rMask == 0x00FF0000 && gMask == 0x0000FF00 && bMask == 0x000000FF && aMask == 0xFF000000)
                {
                    return 28;
                }
            }
            throw new InvalidDataException("Unsupported DDS pixel format.");
        }
    }

    public int ImageCount
    {
        get
        {
            var pixelFormatFlags = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(80, 4));
            if ((pixelFormatFlags & DdpfFourCc) != 0)
            {
                var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(84, 4));
                if (fourCc == FourCcDx10)
                {
                    return BinaryPrimitives.ReadInt32LittleEndian(Data.Span.Slice(140, 4));
                }
            }
            return 1;
        }
    }

    public int MipCount
    {
        get
        {
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(8, 4));
            var mipCount = BinaryPrimitives.ReadInt32LittleEndian(Data.Span.Slice(28, 4));
            var hasMipMaps = (flags & DdsdMipmapCount) != 0 && mipCount > 0;
            return hasMipMaps ? mipCount : 1;
        }
    }

    public byte[][] MipData
    {
        get
        {
            if (!TryGetFormatInfo(FormatId, out var formatInfo))
                throw new InvalidDataException($"Unsupported DDS format: {FormatId}.");

            var pixelFormatFlags = BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(80, 4));
            var usesDx10 = (pixelFormatFlags & DdpfFourCc) != 0 && BinaryPrimitives.ReadUInt32LittleEndian(Data.Span.Slice(84, 4)) == FourCcDx10;
            var headerLength = 128 + (usesDx10 ? 20 : 0);

            var mipData = new byte[checked(ImageCount * MipCount)][];
            var index = 0;
            var offset = headerLength;
            for (var imageIndex = 0; imageIndex < ImageCount; imageIndex++)
            {
                var mipWidth = Width;
                var mipHeight = Height;
                for (var mipIndex = 0; mipIndex < MipCount; mipIndex++)
                {
                    var size = GetMipSize(mipWidth, mipHeight, formatInfo);
                    if (offset + size > Data.Length)
                        throw new InvalidDataException("DDS pixel data is truncated.");

                    mipData[index++] = Data.Slice(offset, size).ToArray();
                    offset += size;
                    mipWidth = Math.Max(1, mipWidth / 2);
                    mipHeight = Math.Max(1, mipHeight / 2);
                }
            }
            return mipData;
        }
    }


    public DdsFile(ReadOnlyMemory<byte> data)
    {
        if (data.Length < 128)
            throw new InvalidDataException("DDS data is too small.");

        if (BinaryPrimitives.ReadUInt32LittleEndian(data.Span) != Magic)
            throw new InvalidDataException("Not a DDS file.");

        if (BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(4, 4)) != HeaderSize)
            throw new InvalidDataException("Unsupported DDS header size.");

        Data = data;
    }

    public static DdsFile Read(byte[] data) => new(data);

    public byte[] ToBytes() => Data.ToArray();

    public static DdsFile FromTexture(TextureFile texture, TextureConvertOptions? options = null)
    {
        var builder = new Builder
        {
            Width = texture.Width,
            Height = texture.Height,
            FormatId = texture.FormatId,
            ImageCount = texture.ImageCount,
            MipCount = texture.MipCount
        };

        for (var imageIndex = 0; imageIndex < texture.ImageCount; imageIndex++)
        {
            for (var mipIndex = 0; mipIndex < texture.MipCount; mipIndex++)
            {
                builder.MipData.Add(texture.GetMipData(imageIndex, mipIndex, options).ToArray());
            }
        }

        return builder.Build();
    }

    public TextureFile ToTextureFile(int version, TextureConvertOptions? options = null)
    {
        var builder = new TextureFile.Builder
        {
            Version = version,
            Width = (ushort)Width,
            Height = (ushort)Height,
            ImageCount = ImageCount,
            MipCount = MipCount,
            FormatId = FormatId,
            MipData = MipData.Select(x => x.ToArray()).ToList()
        };

        return builder.Build(options);
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

    internal static int GetMipPitch(int width, int height, TextureFormatInfo formatInfo)
    {
        if (formatInfo.IsBlockCompressed)
        {
            var blocksWide = Math.Max(1, (width + 3) / 4);
            return blocksWide * formatInfo.UnitSize;
        }

        return width * formatInfo.UnitSize;
    }

    internal static int GetMipSize(int width, int height, TextureFormatInfo formatInfo)
    {
        if (formatInfo.IsBlockCompressed)
        {
            var blocksWide = Math.Max(1, (width + 3) / 4);
            var blocksHigh = Math.Max(1, (height + 3) / 4);
            return blocksWide * blocksHigh * formatInfo.UnitSize;
        }

        return width * height * formatInfo.UnitSize;
    }

    internal static bool TryGetFormatInfo(uint formatId, out TextureFormatInfo formatInfo)
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

    internal readonly record struct TextureFormatInfo(bool IsBlockCompressed, int UnitSize, uint LegacyFourCc);

    public class Builder
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public uint FormatId { get; set; }
        public int ImageCount { get; set; } = 1;
        public int MipCount { get; set; } = 1;
        public List<byte[]> MipData { get; set; } = new();

        public DdsFile Build()
        {
            if (!TryGetFormatInfo(FormatId, out var formatInfo))
                throw new InvalidDataException($"Unsupported DDS format: {FormatId}.");

            if (MipData.Count != checked(ImageCount * MipCount))
                throw new InvalidDataException("Mip data does not match the image/mip counts.");

            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms))
            {
                var usesDx10 = formatInfo.LegacyFourCc == 0;

                var flags = DdsdCaps | DdsdHeight | DdsdWidth | DdsdPixelFormat;
                flags |= formatInfo.IsBlockCompressed ? DdsdLinearSize : DdsdPitch;
                if (MipCount > 1)
                    flags |= DdsdMipmapCount;

                writer.Write(Magic);
                writer.Write(HeaderSize);
                writer.Write(flags);
                writer.Write(Height);
                writer.Write(Width);
                writer.Write(GetMipSize(Width, Height, formatInfo));
                writer.Write(0);
                writer.Write(MipCount);
                writer.Write(new byte[44]);

                writer.Write(PixelFormatSize);
                if (usesDx10)
                {
                    writer.Write(DdpfFourCc);
                    writer.Write(FourCcDx10);
                    writer.Write(0);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0u);
                }
                else if (formatInfo.LegacyFourCc != 0)
                {
                    writer.Write(DdpfFourCc);
                    writer.Write(formatInfo.LegacyFourCc);
                    writer.Write(0);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0u);
                }
                else
                {
                    writer.Write(DdpfRgb | DdpfAlphaPixels);
                    writer.Write(0u);
                    writer.Write(32);
                    writer.Write(0x00FF0000u);
                    writer.Write(0x0000FF00u);
                    writer.Write(0x000000FFu);
                    writer.Write(0xFF000000u);
                }

                var caps = DdsCapsTexture;
                if (MipCount > 1)
                    caps |= DdsCapsComplex | DdsCapsMipmap;

                writer.Write(caps);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);

                if (usesDx10)
                {
                    writer.Write(FormatId);
                    writer.Write(D3d10ResourceDimensionTexture2D);
                    writer.Write(0u);
                    writer.Write(ImageCount);
                    writer.Write(0u);
                }

                foreach (var mip in MipData)
                    writer.Write(mip);
            }

            return new DdsFile(ms.ToArray());
        }
    }
}
