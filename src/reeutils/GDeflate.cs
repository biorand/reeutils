using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Graphics;

namespace IntelOrca.Biohazard.REEUtils;

public sealed class GDeflate : IGDeflateEncoder
{
    public static GDeflate Instance { get; } = new();

    private GDeflate()
    {
    }

    private const int TileSize = 0x10000;
    private const int MaxTiles = 0xFFFF;

    public byte[] Compress(ReadOnlyMemory<byte> uncompressed)
    {
        using IMemoryOwner<byte> compressed = CompressInternal(uncompressed, 12, out var size);
        return compressed.Memory.Span[..size].ToArray();
    }

    public byte[] Decompress(ReadOnlyMemory<byte> compressed, int uncompressedSize)
    {
        var result = new byte[uncompressedSize];
        if (!DecompressInternal(compressed, result))
            throw new InvalidDataException("Failed to decompress gdeflate payload.");

        return result;
    }

    private static unsafe IMemoryOwner<byte> CompressInternal(ReadOnlyMemory<byte> uncompressed, int level, out int size)
    {
        var tileHeader = new TileStreamHeader
        {
            Id = TileStreamCompressor.GDeflate,
            NumTiles = (ushort)Math.Clamp((uncompressed.Length + TileSize - 1) / TileSize, 1, MaxTiles),
            LastTileSize = uncompressed.Length % TileSize,
        };
        var offset = Unsafe.SizeOf<TileStreamHeader>() + (tileHeader.NumTiles << 2);
        size = Unsafe.SizeOf<TileStreamHeader>() + offset + (uncompressed.Length << 1);
        var pool = MemoryPool<byte>.Shared.Rent(size);

        var compressed = pool.Memory;
        var outputSpan = pool.Memory.Span;
        MemoryMarshal.Write(outputSpan, in tileHeader);

        var tileOffsets = MemoryMarshal.Cast<byte, int>(outputSpan[Unsafe.SizeOf<TileStreamHeader>()..])[..tileHeader.NumTiles];
        var compressedOffset = 0;
        var uncompressedOffset = 0;

        using var uncompressedPin = uncompressed.Pin();
        using var compressedPin = compressed.Pin();

        var compressor = NativeMethods.libdeflate_alloc_gdeflate_compressor(Math.Clamp(level, 1, 12));
        var page = stackalloc GDeflatePage[1];
        try
        {
            for (var tileIndex = 0; tileIndex < tileHeader.NumTiles; tileIndex++)
            {
                var slice = uncompressed[uncompressedOffset..];
                if (slice.Length > TileSize)
                    slice = slice[..TileSize];

                var uncompressedPtr = (nint)uncompressedPin.Pointer + uncompressedOffset;
                uncompressedOffset += slice.Length;

                var outputSlice = compressed[(compressedOffset + offset)..];
                page[0] = new GDeflatePage((nint)compressedPin.Pointer + (compressedOffset + offset), outputSlice.Length);

                var compressedSize = NativeMethods.libdeflate_gdeflate_compress(compressor, uncompressedPtr, slice.Length, page, 1);
                if (compressedSize == 0)
                {
                    size = 0;
                    break;
                }

                compressedOffset += (int)compressedSize;
                if (tileIndex < tileHeader.NumTiles - 1)
                {
                    tileOffsets[tileIndex + 1] = compressedOffset;
                }
                else
                {
                    tileOffsets[0] = (int)compressedSize;
                    var newSize = (int)(compressedOffset + offset + compressedSize);
                    if (newSize > size)
                        throw new IndexOutOfRangeException();

                    size = newSize;
                }
            }

            return pool;
        }
        finally
        {
            NativeMethods.libdeflate_free_gdeflate_compressor(compressor);
        }
    }

    private static unsafe bool DecompressInternal(ReadOnlyMemory<byte> compressed, Memory<byte> uncompressed)
    {
        uncompressed.Span.Clear();
        var compressedSpan = compressed.Span;
        var tileHeader = MemoryMarshal.Read<TileStreamHeader>(compressedSpan);
        if (!tileHeader.Valid || tileHeader.Id != TileStreamCompressor.GDeflate)
            return false;

        var tileOffsets = MemoryMarshal.Cast<byte, int>(compressedSpan[Unsafe.SizeOf<TileStreamHeader>()..])[..tileHeader.NumTiles];
        var offset = Unsafe.SizeOf<TileStreamHeader>() + (tileHeader.NumTiles << 2);
        var pages = stackalloc GDeflatePage[tileHeader.NumTiles];
        var safePages = new Span<GDeflatePage>(pages, tileHeader.NumTiles);
        using var compressedPin = compressed.Pin();

        for (var tileIndex = 0; tileIndex < tileHeader.NumTiles; tileIndex++)
        {
            var tileOffset = tileIndex > 0 ? tileOffsets[tileIndex] : 0;
            var tileSize = tileIndex < tileHeader.NumTiles - 1 ? tileOffsets[tileIndex + 1] - tileOffset : tileOffsets[0];
            _ = compressed.Slice(offset, tileSize);
            safePages[tileIndex] = new GDeflatePage((nint)compressedPin.Pointer + offset, tileSize);
            offset += tileSize;
        }

        using var decompressedPin = uncompressed.Pin();
        var decompressor = NativeMethods.libdeflate_alloc_gdeflate_decompressor();
        try
        {
            var result = NativeMethods.libdeflate_gdeflate_decompress(decompressor, pages, tileHeader.NumTiles, (nint)decompressedPin.Pointer, uncompressed.Length, out _);
            return result == 0;
        }
        finally
        {
            NativeMethods.libdeflate_free_gdeflate_decompressor(decompressor);
        }
    }

    private enum TileStreamCompressor : byte
    {
        GDeflate = 4,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
    private record struct TileStreamHeader
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
            readonly get => NumTiles * TileSize - (LastTileSize == 0 ? 0 : TileSize - LastTileSize);
            set
            {
                NumTiles = (ushort)(value / TileSize);
                LastTileSize = value - NumTiles * TileSize;
                if (LastTileSize > 0)
                    NumTiles++;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private readonly record struct GDeflatePage(nint Data, int Size);

    private static class NativeMethods
    {
        private const string LibName = "libgdeflate";

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint libdeflate_alloc_gdeflate_compressor(int level);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe nint libdeflate_gdeflate_compress(nint compressor, nint src, nint srcSize, GDeflatePage* pages, nint numPages);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void libdeflate_free_gdeflate_compressor(nint compressor);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint libdeflate_alloc_gdeflate_decompressor();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe int libdeflate_gdeflate_decompress(nint decompressor, GDeflatePage* pages, nint numPages, nint dst, nint dstSize, out nint bytes);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void libdeflate_free_gdeflate_decompressor(nint decompressor);
    }
}
