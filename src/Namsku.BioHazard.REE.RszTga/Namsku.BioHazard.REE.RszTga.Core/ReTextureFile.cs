using System.Runtime.InteropServices;
using DirectXTexNet;

namespace Namsku.BioHazard.REE.RszTga.Core;

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

    public void ExportToTga(string outputPath, Stream stream)
    {
        uint formatId = 0;
        if (HeaderV2.HasValue) formatId = HeaderV2.Value.Format;
        else if (HeaderV1.HasValue) formatId = HeaderV1.Value.Format;
        
        var dxgiFormat = (DXGI_FORMAT)formatId;

        if (Mips.Count == 0) throw new InvalidDataException("No mips found");
        
        var mainMip = Mips[0];
        stream.Seek((long)mainMip.Offset, SeekOrigin.Begin);
        byte[] data = new byte[mainMip.Size];
        stream.ReadExactly(data, 0, data.Length);

        var metadata = new TexMetadata(
            Header.Width, Header.Height, 1, 1, 1, 0, 0, dxgiFormat, TEX_DIMENSION.TEXTURE2D);

        using var scratch = CheckFormatAndLoad(data, metadata, mainMip.Pitch);
        
        // Decompress to R8G8B8A8 for TGA
        // Check if source is SRGB to preserve gamma
        var targetFmt = DXGI_FORMAT.R8G8B8A8_UNORM;
        if (TexHelper.Instance.IsSRGB(dxgiFormat))
            targetFmt = DXGI_FORMAT.R8G8B8A8_UNORM_SRGB;
            
        using var decompressed = scratch.Decompress(targetFmt);
        var image = decompressed.GetImage(0, 0, 0);
        
        // Manual TGA Write
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);
        
        // Header
        writer.Write((byte)0); // ID Length
        writer.Write((byte)0); // ColorMapType
        writer.Write((byte)2); // ImageType (Uncompressed TrueColor)
        writer.Write((short)0); // ColorMapSpec
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        
        writer.Write((ushort)0); // X Origin
        writer.Write((ushort)0); // Y Origin
        writer.Write((ushort)image.Width);
        writer.Write((ushort)image.Height);
        writer.Write((byte)32); // BPP
        // Match Noesis Header: Byte 17 = 0 implies 0 alpha bits, Bottom-Left origin.
        // If we want to match exactly byte-for-byte.
        // Assuming 32bpp contains alpha but descriptor says 0 bits.
        writer.Write((byte)0); // Descriptor
        
        // Data
        // Row-by-row writing
        // Noesis uses Bottom-Left origin (implied by 0x00 descriptor).
        // So we write Bottom-Up (y from Height-1 down to 0).
        
        byte[] row = new byte[image.Width * 4];
        for (int y = (int)image.Height - 1; y >= 0; y--)
        {
            unsafe
            {
                byte* src = (byte*)image.Pixels + (y * image.RowPitch);
                Marshal.Copy((nint)src, row, 0, row.Length);
            }

            // Swap R and B channels (RGBA -> BGRA) because TGA expects BGR
            for (int x = 0; x < row.Length; x += 4)
            {
                byte temp = row[x];
                row[x] = row[x + 2];
                row[x + 2] = temp;
            }

            writer.Write(row);
        }
    }

    private ScratchImage CheckFormatAndLoad(byte[] data, TexMetadata metadata, uint pitch)
    {
        var scratch = TexHelper.Instance.Initialize2D(metadata.Format, metadata.Width, metadata.Height, 1, 1, CP_FLAGS.NONE);
        
        unsafe
        {
            var image = scratch.GetImage(0, 0, 0);
            Marshal.Copy(data, 0, (nint)image.Pixels, Math.Min(data.Length, (int)image.SlicePitch));
        }
        return scratch;
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
            Mips[i] = mip; 
            
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

    public void ImportFromImage(string inputPath, DXGI_FORMAT targetFormat = DXGI_FORMAT.UNKNOWN, int version = 36)
    {
         ScratchImage scratch;
         var ext = Path.GetExtension(inputPath).ToLower();
         if (ext == ".tga")
             scratch = TexHelper.Instance.LoadFromTGAFile(inputPath);
         else if (ext == ".png" || ext == ".jpg" || ext == ".bmp")
             scratch = TexHelper.Instance.LoadFromWICFile(inputPath, WIC_FLAGS.NONE);
         else if (ext == ".dds")
             scratch = TexHelper.Instance.LoadFromDDSFile(inputPath, DDS_FLAGS.NONE);
         else
             throw new NotSupportedException($"Extension {ext} not supported for import");
             
         using (scratch)
         {
             var meta = scratch.GetMetadata();
             
             ScratchImage processed = scratch;
             bool isDecompressed = false;
             
             if (targetFormat != DXGI_FORMAT.UNKNOWN && targetFormat != meta.Format)
             {
                 if (TexHelper.Instance.IsCompressed(targetFormat))
                 {
                      processed = scratch.Compress(targetFormat, TEX_COMPRESS_FLAGS.DEFAULT, 0.5f);
                      isDecompressed = true;
                 }
                 else
                 {
                      processed = scratch.Convert(targetFormat, TEX_FILTER_FLAGS.DEFAULT, 0.5f);
                      isDecompressed = true;
                 }
             }
             
             Header = new ReTextureHeaderCommon
             {
                 Magic = MAGIC_TEX,
                 Version = (uint)version,
                 Width = (ushort)meta.Width,
                 Height = (ushort)meta.Height,
                 Unk00 = 0
             };
             
             var resultMeta = processed.GetMetadata();
             int mipCount = resultMeta.MipLevels;
             int numImages = resultMeta.ArraySize;
             
             Mips.Clear();
             var dataList = new List<byte[]>();
             
             for (int item = 0; item < resultMeta.ArraySize; item++)
             {
                 for (int level = 0; level < resultMeta.MipLevels; level++)
                 {
                      var image = processed.GetImage(level, item, 0);
                      
                      int size = (int)image.SlicePitch;
                      byte[] buffer = new byte[size];
                      unsafe
                      {
                           Marshal.Copy((nint)image.Pixels, buffer, 0, size);
                      }
                      
                      dataList.Add(buffer);
                      
                      Mips.Add(new ReTextureMip
                      {
                           Offset = 0,
                           Pitch = (uint)image.RowPitch,
                           Size = (uint)size
                      });
                 }
             }
             
             MipData = dataList.ToArray();
             
             uint fmt = (uint)resultMeta.Format;
             if (version == 143221013) version = 36;
             
             if (version > 27)
             {
                 HeaderV2 = new ReTextureHeaderV2
                 {
                     NumImages = (byte)numImages,
                     OneImgMipHdrSize = (byte)(mipCount * 16),
                     Format = fmt,
                     Unk02 = 0, Unk03 = 0, Unk04 = 0
                 };
             }
             else
             {
                 HeaderV1 = new ReTextureHeaderV1
                 {
                     MipCount = (byte)mipCount,
                     NumImages = (byte)numImages,
                     Format = fmt,
                     Unk02 = 0, Unk03 = 0, Unk04 = 0
                 };
             }
             
             // If processed was created by Compress/Convert, we must dispose it.
             // If processed == scratch, do NOT dispose it here (used by wrapping 'using').
             if (isDecompressed)
                 processed.Dispose();
         }
    }
}
