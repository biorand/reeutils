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
            var dds = DdsFile.Read(file.ToDdsBytes());

            Assert.Single(dds.MipData);
            Assert.True(dds.MipData[0].SequenceEqual(expectedMipData));
        }

        [Fact]
        public void Exports_GDeflate_Packed_Mip_Data_To_Dds_When_Codec_Provided()
        {
            var expectedMipData = Enumerable.Range(0, 64).Select(x => (byte)x).ToArray();
            var paddedMipData = BuildPaddedMipData(expectedMipData);
            var codec = new FakeGDeflateCodec();
            var file = new TextureFile(BuildPackedTextureFile(codec.Compress(paddedMipData), 48, paddedMipData.Length));
            var dds = DdsFile.Read(file.ToDdsBytes(codec));

            Assert.Single(dds.MipData);
            Assert.True(dds.MipData[0].SequenceEqual(expectedMipData));
        }

        [Fact]
        public void Builds_Packed_GDeflate_Tex_From_Dds_When_Codec_Provided()
        {
            var expectedMipData = Enumerable.Range(0, 64).Select(x => (byte)x).ToArray();
            var source = new TextureFile(BuildPackedTextureFile(expectedMipData));
            var dds = DdsFile.Read(source.ToDdsBytes());
            var codec = new FakeGDeflateCodec();

            var packedTexBytes = dds.ToTextureBytes(250813143, codec);
            var packedTex = new TextureFile(packedTexBytes);

            Assert.True(packedTex.UsesPackedMips);
            Assert.True(packedTex.GetMipData(gdeflate: codec).Span.SequenceEqual(expectedMipData));
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

        private sealed class FakeGDeflateCodec : IGDeflateCodec
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
