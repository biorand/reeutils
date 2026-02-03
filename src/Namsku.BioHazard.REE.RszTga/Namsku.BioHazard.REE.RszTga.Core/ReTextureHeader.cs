using System.Runtime.InteropServices;

namespace Namsku.BioHazard.REE.RszTga.Core;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ReTextureHeaderCommon
{
    public uint Magic; // 0x00584554 "TEX\0"
    public uint Version;
    public ushort Width;
    public ushort Height;
    public ushort Unk00;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ReTextureHeaderV1
{
    // For Version <= 27
    public byte MipCount;
    public byte NumImages;
    public uint Format;
    public uint Unk02;
    public uint Unk03;
    public uint Unk04;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ReTextureHeaderV2
{
    // For Version > 27
    public byte NumImages;
    public byte OneImgMipHdrSize;
    // 8 bytes padding skipped in Python logic
    public uint Format;
    public uint Unk02;
    public uint Unk03;
    public uint Unk04;
}

public struct ReTextureMip
{
    public ulong Offset;
    public uint Pitch;
    public uint Size;
}
