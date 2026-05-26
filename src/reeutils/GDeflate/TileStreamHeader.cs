using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REEUtils.GDeflate;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
internal record struct TileStreamHeader
{
    public TileStreamHeader()
    {
        Flags = 1;
    }

    public TileStreamCompressor Id
    {
        readonly get;
        set
        {
            field = value;
            Magic = (byte)(0xFF ^ (byte)value);
        }
    }

    public byte Magic { get; private set; }
    public ushort NumTiles { readonly get; set; }
    public uint Flags { readonly get; set; }

    public int LastTileSize
    {
        readonly get => (int)((Flags >> 2) & 0x3FFFFU);
        set => Flags = (Flags & 0xFFF00003U) | (((uint)value & 0x3FFFFU) << 2);
    }

    public readonly bool Valid => (byte)Id == (0xFF ^ Magic);

    public int UncompressedSize
    {
        readonly get => NumTiles * VendoredGDeflate.TileSize - (LastTileSize == 0 ? 0 : VendoredGDeflate.TileSize - LastTileSize);
        set
        {
            NumTiles = (ushort)(value / VendoredGDeflate.TileSize);
            LastTileSize = value - NumTiles * VendoredGDeflate.TileSize;
            if (LastTileSize > 0)
                NumTiles++;
        }
    }
}
