using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REEUtils.GDeflate;

internal static class VendoredGDeflate
{
    public const int TileSize = 0x10000;
    public const int MaxTiles = 0xFFFF;

    public static unsafe IMemoryOwner<byte> Compress(ReadOnlyMemory<byte> uncompressed, int level, out int size)
    {
        NativeMethods.EnsureLoaded();

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

    public static unsafe bool Decompress(ReadOnlyMemory<byte> compressed, Memory<byte> uncompressed)
    {
        NativeMethods.EnsureLoaded();

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
}
