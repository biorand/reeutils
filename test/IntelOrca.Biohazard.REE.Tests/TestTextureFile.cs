using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.REE.Graphics;

namespace IntelOrca.Biohazard.REE.Tests
{
    public sealed class TestTextureFile
    {
        [Fact]
        public void Reads_Metadata_And_Raw_Mip_Data()
        {
            var data = BuildTextureFile();
            var file = new TextureFile(data);

            Assert.Equal(143221013u, file.RawVersion);
            Assert.Equal(36, file.EffectiveVersion);
            Assert.Equal((ushort)64, file.Width);
            Assert.Equal((ushort)32, file.Height);
            Assert.Equal(98u, file.FormatId);
            Assert.Equal(TextureCompression.Bc7Unorm, file.Compression);
            Assert.Equal(1, file.ImageCount);
            Assert.Equal(1, file.MipCount);
            Assert.Equal(1, file.TotalMipCount);
            Assert.True(file.GetMipData().Span.SequenceEqual(new byte[] { 1, 2, 3, 4 }));
        }

        [Fact]
        public void Reads_Packed_Mip_Data_And_Strips_Block_Padding()
        {
            var expectedMipData = Enumerable.Range(0, 64).Select(x => (byte)x).ToArray();
            var file = new TextureFile(BuildPackedTextureFile(expectedMipData));

            Assert.True(file.UsesPackedMips);
            Assert.Equal((uint)250813143, file.RawVersion);
            Assert.Equal(TextureCompression.Bc7UnormSrgb, file.Compression);
            Assert.True(file.GetMipData().Span.SequenceEqual(expectedMipData));
        }

        [Fact]
        public void Exports_Packed_Mip_Data_To_Dds()
        {
            var expectedMipData = Enumerable.Range(0, 64).Select(x => (byte)x).ToArray();
            var file = new TextureFile(BuildPackedTextureFile(expectedMipData));
            var dds = file.ToDds();

            Assert.Single(dds.MipData);
            Assert.True(dds.MipData[0].SequenceEqual(expectedMipData));
        }

        [Fact]
        public void Exports_GDeflate_Packed_Mip_Data_To_Dds_When_Codec_Provided()
        {
            var expectedMipData = Enumerable.Range(0, 64).Select(x => (byte)x).ToArray();
            var paddedMipData = BuildPaddedMipData(expectedMipData);
            var encoder = new FakeGDeflateEncoder();
            var options = new TextureConvertOptions { Encoder = encoder };
            var file = new TextureFile(BuildPackedTextureFile(encoder.Compress(paddedMipData), 48, paddedMipData.Length));
            var dds = file.ToDds(options);

            Assert.Single(dds.MipData);
            Assert.True(dds.MipData[0].SequenceEqual(expectedMipData));
        }

        [Fact]
        public void Builds_Packed_GDeflate_Tex_From_Dds_When_Codec_Provided()
        {
            var expectedMipData = Enumerable.Range(0, 64).Select(x => (byte)x).ToArray();
            var source = new TextureFile(BuildPackedTextureFile(expectedMipData));
            var dds = source.ToDds();
            var encoder = new FakeGDeflateEncoder();
            var options = new TextureConvertOptions { Encoder = encoder };

            var packedTex = dds.ToTextureFile(250813143, options);

            Assert.True(packedTex.UsesPackedMips);
            Assert.True(packedTex.GetMipData(options: options).Span.SequenceEqual(expectedMipData));
        }

        private static byte[] BuildTextureFile()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(0x00584554u);
            bw.Write(143221013u);
            bw.Write((ushort)64);
            bw.Write((ushort)32);
            bw.Write((ushort)0);
            bw.Write((byte)1);
            bw.Write((byte)16);
            bw.Write(98u);
            bw.Write(0u);
            bw.Write(0u);
            bw.Write(0u);
            bw.Write(new byte[8]);
            bw.Write(56UL);
            bw.Write(256u);
            bw.Write(4u);
            bw.Write(new byte[] { 1, 2, 3, 4 });

            return ms.ToArray();
        }

        private static byte[] BuildPackedTextureFile(byte[] expectedMipData)
        {
            var paddedMipData = BuildPaddedMipData(expectedMipData);
            return BuildPackedTextureFile(paddedMipData, 48, paddedMipData.Length);
        }

        private static byte[] BuildPackedTextureFile(byte[] storedPayload, uint pitch, int size)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(0x00584554u);
            bw.Write(250813143u);
            bw.Write((ushort)6);
            bw.Write((ushort)5);
            bw.Write((ushort)0);
            bw.Write((byte)1);
            bw.Write((byte)16);
            bw.Write(99u);
            bw.Write(0u);
            bw.Write(0u);
            bw.Write(0u);
            bw.Write(new byte[8]);

            bw.Write(56UL);
            bw.Write(pitch);
            bw.Write((uint)size);

            bw.Write((uint)storedPayload.Length);
            bw.Write(0u);
            bw.Write(storedPayload);

            return ms.ToArray();
        }

        private static byte[] BuildPaddedMipData(byte[] expectedMipData)
        {
            var rowPadding = Enumerable.Repeat((byte)0xEE, 16).ToArray();
            return expectedMipData[..32]
                .Concat(rowPadding)
                .Concat(expectedMipData[32..])
                .Concat(rowPadding)
                .ToArray();
        }

        [Theory]
        [InlineData("natives/x64/objectroot/setmodel/textures/sm70_201_inkribbon01a_a_albm.tex.10", 1024, 1024, 873095586)]
        [InlineData("natives/x64/objectroot/setmodel/textures/sm70_201_inkribbon01a_a_nrmr.tex.10", 512, 512, -250761442)]
        [InlineData("natives/x64/streaming/objectroot/setmodel/textures/sm70_201_inkribbon01a_a_albm.tex.10", 2048, 2048, -2134722669)]
        [InlineData("natives/x64/streaming/objectroot/setmodel/textures/sm70_201_inkribbon01a_a_nrmr.tex.10", 2048, 2048, -214742397)]
        public void Reads_RE2R_InkRibbon_Textures(string path, int expectedWidth, int expectedHeight, int expectedDdsHash)
        {
            var data = OriginalPakHelper.Default.GetFileData(GameNames.RE2, path);
            var file = new TextureFile(data);

            Assert.Equal(10u, file.RawVersion);
            Assert.Equal(10, file.EffectiveVersion);
            Assert.Equal(expectedWidth, file.Width);
            Assert.Equal(expectedHeight, file.Height);

            // Export to DDS
            var dds = file.ToDds();
            var ddsBytes = dds.ToBytes();
            var ddsHash = IntelOrca.Biohazard.REE.Cryptography.MurMur3.HashData(ddsBytes);
            Assert.Equal(expectedDdsHash, ddsHash);

            // Triple-trip check: converting DDS -> TEX -> DDS produces an identical DDS
            var roundtripTex = dds.ToTextureFile((int)file.RawVersion);
            var roundtripDds = roundtripTex.ToDds();
            Assert.True(dds.ToBytes().SequenceEqual(roundtripDds.ToBytes()));

            Assert.Equal(file.Width, roundtripTex.Width);
            Assert.Equal(file.Height, roundtripTex.Height);
            Assert.Equal(file.RawVersion, roundtripTex.RawVersion);
            Assert.Equal(file.Compression, roundtripTex.Compression);
            Assert.Equal(file.MipCount, roundtripTex.MipCount);
        }

        [Fact]
        public void Reads_RE9_Title_Texture()
        {
            var data = OriginalPakHelper.Default.GetFileData(GameNames.RE9, "natives/stm/gui/ui1000/tex/ui1000_iam.tex.250813143");
            var file = new TextureFile(data);

            Assert.Equal(250813143u, file.RawVersion);
            Assert.Equal(250813143, file.EffectiveVersion);
            Assert.Equal((ushort)2048, file.Width);
            Assert.Equal((ushort)1024, file.Height);
            Assert.Equal(99u, file.FormatId);
            Assert.Equal(TextureCompression.Bc7UnormSrgb, file.Compression);
            Assert.Equal(1, file.ImageCount);
            Assert.Equal(1, file.MipCount);
            Assert.Equal(1, file.TotalMipCount);
            Assert.True(file.UsesPackedMips);

            var encoder = new MockGDeflateEncoder(data);
            var options = new TextureConvertOptions { Encoder = encoder };
            var dds = file.ToDds(options);

            Assert.Single(dds.MipData);

            var decompHash = IntelOrca.Biohazard.REE.Cryptography.MurMur3.HashData(dds.MipData[0]);
            Assert.Equal(1662662843, decompHash);

            // Triple-trip check using mock codec: DDS -> TEX -> DDS produces an identical DDS
            var roundtripTex = dds.ToTextureFile((int)file.RawVersion, options);
            var roundtripDds = roundtripTex.ToDds(options);
            Assert.True(dds.ToBytes().SequenceEqual(roundtripDds.ToBytes()));

            Assert.Equal(file.Width, roundtripTex.Width);
            Assert.Equal(file.Height, roundtripTex.Height);
            Assert.Equal(file.RawVersion, roundtripTex.RawVersion);
            Assert.Equal(file.Compression, roundtripTex.Compression);
            Assert.Equal(file.MipCount, roundtripTex.MipCount);
        }

        private sealed class MockGDeflateEncoder : IGDeflateEncoder
        {
            private static readonly byte[] DecompressedData;
            private readonly byte[] _compressedMip;

            static MockGDeflateEncoder()
            {
                using var ms = new MemoryStream(Resources.ui1000_iam_decompressed);
                using var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                gzip.CopyTo(outMs);
                DecompressedData = outMs.ToArray();
            }

            public MockGDeflateEncoder(byte[] originalTextureBytes)
            {
                _compressedMip = new byte[originalTextureBytes.Length - 64];
                Array.Copy(originalTextureBytes, 64, _compressedMip, 0, _compressedMip.Length);
            }

            public byte[] Compress(ReadOnlyMemory<byte> uncompressed)
            {
                var hash = IntelOrca.Biohazard.REE.Cryptography.MurMur3.HashData(uncompressed.ToArray());
                if (hash == 1662662843)
                {
                    return _compressedMip;
                }
                throw new NotSupportedException($"MockGDeflateEncoder: Unexpected uncompressed payload to compress. MurMur3: {hash}");
            }

            public byte[] Decompress(ReadOnlyMemory<byte> compressed, int uncompressedSize)
            {
                var hash = IntelOrca.Biohazard.REE.Cryptography.MurMur3.HashData(compressed.ToArray());
                if (hash == 342356049)
                {
                    return DecompressedData;
                }
                throw new NotSupportedException($"MockGDeflateEncoder: Unexpected compressed payload to decompress. MurMur3: {hash}");
            }
        }

        private sealed class FakeGDeflateEncoder : IGDeflateEncoder
        {
            private readonly Dictionary<string, byte[]> _payloads = new();
            private int _nextId = 1;

            public byte[] Compress(ReadOnlyMemory<byte> uncompressed)
            {
                var id = _nextId++;
                var compressed = new byte[8];
                compressed[0] = 0x04;
                compressed[1] = 0xFB;
                BinaryPrimitives.WriteInt32LittleEndian(compressed.AsSpan(4), id);
                _payloads[Convert.ToHexString(compressed)] = uncompressed.ToArray();
                return compressed;
            }

            public byte[] Decompress(ReadOnlyMemory<byte> compressed, int uncompressedSize)
            {
                var key = Convert.ToHexString(compressed.ToArray());
                return _payloads[key];
            }
        }
    }
}
