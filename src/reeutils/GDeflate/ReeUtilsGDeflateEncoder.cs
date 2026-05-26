using System;
using System.Buffers;
using System.IO;
using IntelOrca.Biohazard.REE.Graphics;

namespace IntelOrca.Biohazard.REEUtils.GDeflate;

public sealed class ReeUtilsGDeflateEncoder : IGDeflateEncoder
{
    public static ReeUtilsGDeflateEncoder Instance { get; } = new();

    private ReeUtilsGDeflateEncoder()
    {
    }

    public byte[] Compress(ReadOnlyMemory<byte> uncompressed)
    {
        using IMemoryOwner<byte> compressed = VendoredGDeflate.Compress(uncompressed, 12, out var size);
        return compressed.Memory.Span[..size].ToArray();
    }

    public byte[] Decompress(ReadOnlyMemory<byte> compressed, int uncompressedSize)
    {
        var result = new byte[uncompressedSize];
        if (!VendoredGDeflate.Decompress(compressed, result))
            throw new InvalidDataException("Failed to decompress gdeflate payload.");

        return result;
    }
}
