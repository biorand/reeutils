#if NET6_0_OR_GREATER
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BCnEncoder.Shared;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace IntelOrca.Biohazard.REE.Textures;

public class ReTextureFile
{
    public const uint MAGIC_TEX = 0x00584554; // "TEX\0"
    public ReTextureHeaderCommon Header;
    public ReTextureHeaderV1? HeaderV1;
    public ReTextureHeaderV2? HeaderV2;
    public List<ReTextureMip> Mips = new();
    public byte[][]? MipData;

    public int Version => (int)Header.Version;

    public void Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.Default, true);
        Header = new ReTextureHeaderCommon
        {
            Magic = reader.ReadUInt32(),
            Version = reader.ReadUInt32(),
            Width = reader.ReadUInt16(),
            Height = reader.ReadUInt16(),
            Unk00 = reader.ReadUInt16()
        };

        if (Header.Magic != MAGIC_TEX)
            throw new InvalidDataException($"Invalid magic: {Header.Magic:X}");

        var effectiveVersion = Header.Version;
        if (effectiveVersion == 190820018) effectiveVersion = 10;
        if (effectiveVersion == 143221013) effectiveVersion = 36;

        int mipCount = 0;
        int numImages = 0;

        if (effectiveVersion > 27)
        {
            HeaderV2 = new ReTextureHeaderV2
            {
                NumImages = reader.ReadByte(),
                OneImgMipHdrSize = reader.ReadByte(),
            };
            
            mipCount = HeaderV2.Value.OneImgMipHdrSize / 16;
            numImages = HeaderV2.Value.NumImages;
            
            var h2 = HeaderV2.Value;
            h2.Format = reader.ReadUInt32();
            h2.Unk02 = reader.ReadUInt32();
            h2.Unk03 = reader.ReadUInt32();
            h2.Unk04 = reader.ReadUInt32();
            HeaderV2 = h2;

            reader.BaseStream.Seek(8, SeekOrigin.Current); // Padding
        }
        else
        {
            HeaderV1 = new ReTextureHeaderV1
            {
                MipCount = reader.ReadByte(),
                NumImages = reader.ReadByte(),
                Format = reader.ReadUInt32(),
                Unk02 = reader.ReadUInt32(),
                Unk03 = reader.ReadUInt32(),
                Unk04 = reader.ReadUInt32()
            };
            mipCount = HeaderV1.Value.MipCount;
            numImages = HeaderV1.Value.NumImages;
        }

        Mips.Clear();
        
        for (int i = 0; i < numImages; i++)
        {
            for (int j = 0; j < mipCount; j++)
            {
                var mip = new ReTextureMip
                {
                    Offset = reader.ReadUInt64(),
                    Pitch = reader.ReadUInt32(),
                    Size = reader.ReadUInt32()
                };
                Mips.Add(mip);
            }
        }
    }

    public byte[] GetDecompressedData(Stream stream, out int width, out int height)
    {
        width = Header.Width;
        height = Header.Height;
        
        uint formatId = 0;
        if (HeaderV2.HasValue) formatId = HeaderV2.Value.Format;
        else if (HeaderV1.HasValue) formatId = HeaderV1.Value.Format;
        
        var compressionFormat = GetCompressionFormat(formatId);

        if (Mips.Count == 0) throw new InvalidDataException("No mips found");
        
        var mainMip = Mips[0];
        stream.Seek((long)mainMip.Offset, SeekOrigin.Begin);
        byte[] data = new byte[mainMip.Size];
        stream.ReadExactly(data, 0, data.Length);

        // Decompress using BCnEncoder
        var decoder = new BcDecoder();
        
        byte[] rawBytes;

        if (compressionFormat == CompressionFormat.Unknown)
        {
             // For now, if format is R8G8B8A8 (28) or SRGB (29), simple copy
             if (formatId == 28 || formatId == 29) // R8G8B8A8
                 rawBytes = data;
             else
                 throw new NotSupportedException($"Format {formatId} not supported by BCnEncoder mapping yet.");
        }
        else
        {
             var colors = decoder.DecodeRaw(data, Header.Width, Header.Height, compressionFormat);
             rawBytes = new byte[colors.Length * 4];
             MemoryMarshal.Cast<ColorRgba32, byte>(colors).CopyTo(rawBytes);
        }
        return rawBytes;
    }

    public void ExportToTga(string outputPath, Stream stream)
    {
        var rawBytes = GetDecompressedData(stream, out _, out _);
        
        // Manual TGA Write (Bottom-Up)
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);
        
        // Header
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)2); // TrueColor
        writer.Write((short)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)Header.Width);
        writer.Write((ushort)Header.Height);
        writer.Write((byte)32); // 32 bpp
        writer.Write((byte)0); // Descriptor: Bottom-Left
        
        // Data Copy with Vertical Flip
        // pitch = width * 4
        int rowPitch = Header.Width * 4;
        byte[] row = new byte[rowPitch];
        
        for (int y = (int)Header.Height - 1; y >= 0; y--)
        {
            Array.Copy(rawBytes, y * rowPitch, row, 0, rowPitch);
            
            // Swap R/B? BCnEncoder Decodes to R,G,B,A (ColorRgba32)
            // TGA expects B,G,R,A
            for (int x = 0; x < row.Length; x += 4)
            {
                byte temp = row[x];
                row[x] = row[x+2];
                row[x+2] = temp;
            }
            
            writer.Write(row);
        }
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.Default, true);
        
        writer.Write(Header.Magic);
        writer.Write(Header.Version);
        writer.Write(Header.Width);
        writer.Write(Header.Height);
        writer.Write(Header.Unk00);

        int effectiveVersion = Version;
        if (effectiveVersion == 190820018) effectiveVersion = 10;
        if (effectiveVersion == 143221013) effectiveVersion = 36;
        
        if (effectiveVersion > 27)
        {
             if (!HeaderV2.HasValue) throw new InvalidOperationException("HeaderV2 missing for version " + Version);
             var v2 = HeaderV2.Value;
             
             writer.Write(v2.NumImages);
             writer.Write(v2.OneImgMipHdrSize);
             
             writer.Write(v2.Format);
             writer.Write(v2.Unk02);
             writer.Write(v2.Unk03);
             writer.Write(v2.Unk04);
             
             writer.Write(new byte[8]);
        }
        else
        {
             if (!HeaderV1.HasValue) throw new InvalidOperationException("HeaderV1 missing for version " + Version);
             var v1 = HeaderV1.Value;
             writer.Write(v1.MipCount);
             writer.Write(v1.NumImages);
             writer.Write(v1.Format);
             writer.Write(v1.Unk02);
             writer.Write(v1.Unk03);
             writer.Write(v1.Unk04);
        }

        long mipHeadStart = stream.Position;
        long dataStartOffset = stream.Position + (Mips.Count * 16);
        long currentOffset = dataStartOffset;
        
        for (int i=0; i < Mips.Count; i++)
        {
            var mip = Mips[i];
            mip.Offset = (ulong)currentOffset;
            Mips[i] = mip; // Update offset
            
            writer.Write(mip.Offset);
            writer.Write(mip.Pitch);
            writer.Write(mip.Size);
            
            currentOffset += mip.Size;
        }
        
        if (MipData == null || MipData.Length != Mips.Count)
             throw new InvalidOperationException("MipData missing or mismatch");
             
        foreach (var data in MipData)
        {
            writer.Write(data);
        }
    }

    public void ImportFromImage(string inputPath, uint? targetFormatId = null, int version = 36)
    {
        // Load using ImageSharp
        using var image = Image.Load<Rgba32>(inputPath);
        
        // Determine target format
        CompressionFormat targetFormat = CompressionFormat.Bc7; // Default to BC7
        uint finalFormatId = 98; // BC7_UNORM
        
        if (targetFormatId.HasValue)
        {
            finalFormatId = targetFormatId.Value;
            targetFormat = GetCompressionFormat(finalFormatId);
            if (targetFormat == CompressionFormat.Unknown)
                 throw new NotSupportedException($"Format ID {finalFormatId} is not supported for compression.");
        }
        
        var encoder = new BcEncoder();
        encoder.OutputOptions.GenerateMipMaps = true;
        encoder.OutputOptions.Quality = CompressionQuality.Balanced;
        encoder.OutputOptions.Format = targetFormat;
        
        // Manual conversion (Retry with 4 args)
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        
        // Encode with correct overload: (byte[], width, height, PixelFormat)
        // Ensure OutputOptions.Format is set (it is above)
        var mipsData = encoder.EncodeToRawBytes(pixels, image.Width, image.Height, PixelFormat.Rgba32);
        
        // Prepare Headers
        MipData = mipsData.ToArray();
        
        Header = new ReTextureHeaderCommon
        {
            Magic = MAGIC_TEX,
            Version = (uint)version,
            Width = (ushort)image.Width,
            Height = (ushort)image.Height,
            Unk00 = 0
        };

        int mipCount = mipsData.Count(); // Compiler says method group, so invocation needed
        int numImages = 1;
        int currentWidth = image.Width;
        int currentHeight = image.Height;
        
        for (int i = 0; i < mipCount; i++)
        {
            // Calculate size and pitch
            // For block formats (BCn), size is based on 4x4 blocks
            // This is handled by encoder output size, but we need pitch?
            // RE Engine pitch: usually width * bpp? For BCn it's width * blocks?
            // Actually pitch in RE Headers for compressed is often just width * 4 or something similar?
            // Let's approximate standard pitch logic:
            
            // Standard calc: max(1, (width+3)/4) * blockSize
            int blockSize = 16; // BC7, BC1=8
            if (targetFormat == CompressionFormat.Bc1 || targetFormat == CompressionFormat.Bc4) blockSize = 8;
            
            int blocksW = Math.Max(1, (currentWidth + 3) / 4);
            uint pitch = (uint)(blocksW * blockSize);
            
            Mips.Add(new ReTextureMip
            {
                Offset = 0,
                Pitch = pitch,
                Size = (uint)mipsData[i].Length
            });
            
            currentWidth = Math.Max(1, currentWidth / 2);
            currentHeight = Math.Max(1, currentHeight / 2);
        }
        
        if (version == 143221013) version = 36;

        if (version > 27)
        {
            HeaderV2 = new ReTextureHeaderV2
            {
                NumImages = 1,
                OneImgMipHdrSize = (byte)(mipCount * 16),
                Format = finalFormatId,
                Unk02 = 0, Unk03 = 0, Unk04 = 0
            };
        }
        else
        {
            HeaderV1 = new ReTextureHeaderV1
            {
                MipCount = (byte)mipCount,
                NumImages = 1,
                Format = finalFormatId,
                Unk02 = 0, Unk03 = 0, Unk04 = 0
            };
        }
    }

    private CompressionFormat GetCompressionFormat(uint reFormat)
    {
        // Simple mapping based on ReTextureFormat enum
        // DXGI 71 = BC1_UNORM
        // DXGI 72 = BC1_UNORM_SRGB
        // DXGI 74 = BC2_UNORM
        // DXGI 77 = BC3_UNORM
        // DXGI 80 = BC4_UNORM
        // DXGI 83 = BC5_UNORM
        // DXGI 95 = BC6H
        // DXGI 98 = BC7_UNORM
        // DXGI 99 = BC7_UNORM_SRGB
        
        return reFormat switch
        {
            71 => CompressionFormat.Bc1,
            72 => CompressionFormat.Bc1, // Encoder doesn't distinguish SRGB flag usually, handled by metadata
            74 => CompressionFormat.Bc2,
            75 => CompressionFormat.Bc2,
            77 => CompressionFormat.Bc3,
            78 => CompressionFormat.Bc3,
            80 => CompressionFormat.Bc4,
            83 => CompressionFormat.Bc5,
            95 => CompressionFormat.Bc6U, // Unsigned?
            96 => CompressionFormat.Bc6S,
            98 => CompressionFormat.Bc7,
            99 => CompressionFormat.Bc7,
            _ => CompressionFormat.Unknown
        };
    }
}
#endif
