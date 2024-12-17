using System;
using System.IO;
using System.Numerics;

namespace IntelOrca.Biohazard.REE.Cryptography
{
    internal static class ResourceCipher
    {
        private static readonly byte[] g_modulus = [
            0x13, 0xD7, 0x9C, 0x89, 0x88, 0x91, 0x48, 0x10, 0xD7, 0xAA, 0x78, 0xAE, 0xF8, 0x59, 0xDF, 0x7D,
            0x3C, 0x43, 0xA0, 0xD0, 0xBB, 0x36, 0x77, 0xB5, 0xF0, 0x5C, 0x02, 0xAF, 0x65, 0xD8, 0x77, 0x03,
            0x00
        ];

        private static readonly byte[] g_exponent = [
            0xC0, 0xC2, 0x77, 0x1F, 0x5B, 0x34, 0x6A, 0x01, 0xC7, 0xD4, 0xD7, 0x85, 0x2E, 0x42, 0x2B, 0x3B,
            0x16, 0x3A, 0x17, 0x13, 0x16, 0xEA, 0x83, 0x30, 0x30, 0xDF, 0x3F, 0xF4, 0x25, 0x93, 0x20, 0x01,
            0x00
        ];

        public static byte[] DecryptData(byte[] buffer)
        {
            using var br = new BinaryReader(new MemoryStream(buffer));
            var offset = 0;
            var blockCount = (buffer.Length - 8) / 128;
            var decryptedSize = br.ReadInt64();
            var finalResult = new byte[decryptedSize + 1];
            for (int i = 0; i < blockCount; i++, offset += 8)
            {
                var decryptedBlock = ReadKey(br);
                Array.Copy(decryptedBlock, 0, finalResult, offset, decryptedBlock.Length);
            }
            return finalResult;
        }

        private static byte[] ReadKey(BinaryReader br)
        {
            var key = new BigInteger(br.ReadBytes(64));
            var data = new BigInteger(br.ReadBytes(64));
            var modulus = new BigInteger(g_modulus);
            var exponent = new BigInteger(g_exponent);
            var mod = BigInteger.ModPow(key, exponent, modulus);
            var result = BigInteger.Divide(data, mod);
            return result.ToByteArray();
        }
    }
}
