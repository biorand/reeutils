using System;

namespace IntelOrca.Biohazard.REE.Graphics;

public interface IGDeflateEncoder
{
    byte[] Compress(ReadOnlyMemory<byte> uncompressed);
    byte[] Decompress(ReadOnlyMemory<byte> compressed, int uncompressedSize);
}
