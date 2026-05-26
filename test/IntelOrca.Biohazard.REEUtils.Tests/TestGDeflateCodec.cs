using System.IO;
using System.Linq;
using IntelOrca.Biohazard.REE.Graphics;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    public sealed class TestGDeflateCodec
    {
        [Fact]
        public void Codec_RoundTrips_Re9_Texture_Data()
        {
            var expectedMipData = Enumerable.Repeat((byte)0x5A, 64 * 64 * 4).ToArray();
            var texBytes = BuildRgbaTextureFile(expectedMipData);
            var file = new TextureFile(texBytes);
            var dds = file.ToDds();

            var options = new TextureConvertOptions
            {
                Encoder = IntelOrca.Biohazard.REEUtils.GDeflate.Instance
            };
            var packedTexture = dds.ToTextureFile(250813143, options);

            Assert.True(packedTexture.UsesPackedMips);
            Assert.True(packedTexture.GetMipData(options: options).ToArray().SequenceEqual(expectedMipData));

            // Triple-trip check: converting DDS -> TEX -> DDS produces an identical DDS
            var roundtripDds = packedTexture.ToDds(options);
            Assert.True(dds.ToBytes().SequenceEqual(roundtripDds.ToBytes()));
        }

        private static byte[] BuildRgbaTextureFile(byte[] mipData)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(0x00584554u);
            bw.Write(143221013u);
            bw.Write((ushort)64);
            bw.Write((ushort)64);
            bw.Write((ushort)0);
            bw.Write((byte)1);
            bw.Write((byte)16);
            bw.Write(28u);
            bw.Write(0xFFFFFFFFu);
            bw.Write(0u);
            bw.Write(0x0080u);
            bw.Write(new byte[8]);
            bw.Write(56UL);
            bw.Write((uint)(64 * 4));
            bw.Write((uint)mipData.Length);
            bw.Write(mipData);

            return ms.ToArray();
        }
    }
}
