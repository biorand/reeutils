using System;

namespace IntelOrca.Biohazard.REE.Graphics;

public interface IGDeflateCodec
{
    byte[] Compress(ReadOnlyMemory<byte> uncompressed);
    byte[] Decompress(ReadOnlyMemory<byte> compressed, int uncompressedSize);
}
