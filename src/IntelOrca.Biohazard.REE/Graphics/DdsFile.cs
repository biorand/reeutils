using System;
using System.Buffers.Binary;
using System.IO;

namespace IntelOrca.Biohazard.REE.Graphics;

public sealed class DdsFile
{
    private const uint Magic = 0x20534444;
    private const uint TrailerMagic = 0x30524545;
    private const int HeaderSize = 124;
    private const int PixelFormatSize = 32;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public byte[] PixelData { get; private set; } = Array.Empty<byte>();
    public byte[]? OriginalTexBytes { get; private set; }

    public static DdsFile FromTexture(TextureFile texture)
    {
        var pixelData = ExpandToRgba(texture.GetMipData(0, 0).Span, texture.Width * texture.Height * 4);

        return new DdsFile
        {
            Width = texture.Width,
            Height = texture.Height,
            PixelData = pixelData,
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

        _ = reader.ReadUInt32(); // flags
        var height = reader.ReadInt32();
        var width = reader.ReadInt32();
        _ = reader.ReadInt32(); // pitchOrLinearSize
        _ = reader.ReadInt32(); // depth
        _ = reader.ReadInt32(); // mipMapCount
        reader.Advance(44); // reserved1

        if (reader.ReadInt32() != PixelFormatSize)
            throw new InvalidDataException("Unsupported DDS pixel format.");

        var pixelFormatFlags = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // fourCC
        var rgbBitCount = reader.ReadInt32();
        var rMask = reader.ReadUInt32();
        var gMask = reader.ReadUInt32();
        var bMask = reader.ReadUInt32();
        var aMask = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // caps
        _ = reader.ReadUInt32(); // caps2
        _ = reader.ReadUInt32(); // caps3
        _ = reader.ReadUInt32(); // caps4
        _ = reader.ReadUInt32(); // reserved2

        if ((pixelFormatFlags & 0x40) == 0 || rgbBitCount != 32 ||
            rMask != 0x00FF0000 || gMask != 0x0000FF00 || bMask != 0x000000FF || aMask != 0xFF000000)
        {
            throw new InvalidDataException("DDS pixel format is not the expected 32-bit RGBA layout.");
        }

        var pixelDataLength = checked(width * height * 4);
        if (reader.Remaining < pixelDataLength)
            throw new InvalidDataException("DDS pixel data is truncated.");

        var pixelData = reader.ReadBytes(pixelDataLength);
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
            PixelData = pixelData,
            OriginalTexBytes = originalTexBytes,
        };
    }

    public byte[] ToBytes()
    {
        var data = new byte[128 + PixelData.Length + (OriginalTexBytes?.Length is int len ? 8 + len : 0)];
        var writer = new SpanWriter(data);

        writer.WriteUInt32(Magic);
        writer.WriteInt32(HeaderSize);
        writer.WriteUInt32(0x00021007); // caps | height | width | pixelformat | pitch
        writer.WriteInt32(Height);
        writer.WriteInt32(Width);
        writer.WriteInt32(Width * 4);
        writer.WriteInt32(0);
        writer.WriteInt32(1);
        writer.WriteZeros(44);

        writer.WriteInt32(PixelFormatSize);
        writer.WriteUInt32(0x41); // RGB | ALPHAPIXELS
        writer.WriteUInt32(0);
        writer.WriteInt32(32);
        writer.WriteUInt32(0x00FF0000);
        writer.WriteUInt32(0x0000FF00);
        writer.WriteUInt32(0x000000FF);
        writer.WriteUInt32(0xFF000000);

        writer.WriteUInt32(0x1000); // DDSCAPS_TEXTURE
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);

        writer.WriteBytes(PixelData);

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
        if (OriginalTexBytes is null)
            throw new InvalidDataException("DDS file does not contain original TEX data.");

        return OriginalTexBytes;
    }

    private static byte[] ExpandToRgba(ReadOnlySpan<byte> source, int length)
    {
        if (length <= 0)
            return Array.Empty<byte>();

        var result = new byte[length];
        if (source.Length == 0)
            return result;

        var copyLength = Math.Min(source.Length, result.Length);
        source[..copyLength].CopyTo(result);

        if (copyLength == result.Length)
            return result;

        for (var i = copyLength; i < result.Length; i++)
        {
            result[i] = source[i % source.Length];
        }

        return result;
    }

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

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_data.AsSpan(_offset, 4), value);
            _offset += 4;
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
