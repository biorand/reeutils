using System.IO;
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
    }
}
