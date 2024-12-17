using System;
using System.Security.Cryptography;
using System.Text;

namespace IntelOrca.Biohazard.REE.Cryptography
{
    public sealed class MurMur3 : HashAlgorithm
    {
        const uint g_seed = 0xFFFFFFFF;
        const uint c1 = 0xCC9E2D51;
        const uint c2 = 0x1B873593;

        private uint _dataLength;
        private uint _hash = g_seed;

        public override void Initialize()
        {
            _dataLength = 0;
            _hash = g_seed;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            var offset = 0;
            var left = cbSize;
            while (left > 0)
            {
                var chunkLen = Math.Min(left, 4);
                ContinueHash(array, offset, chunkLen);
                offset += chunkLen;
                left -= chunkLen;
            }
        }

        protected override byte[] HashFinal()
        {
            _hash = Fmix(_hash ^ _dataLength);
            return BitConverter.GetBytes(_hash);
        }

        private void ContinueHash(byte[] chunk, int offset, int length)
        {
            _dataLength += (uint)length;
            switch (length)
            {
                case 1:
                    {
                        var k1 = (uint)chunk[offset];
                        k1 *= c1;
                        k1 = rol32(k1, 15);
                        k1 *= c2;
                        _hash ^= k1;
                        break;
                    }
                case 2:
                    {
                        var k1 = (uint)(chunk[offset] | chunk[offset + 1] << 8);
                        k1 *= c1;
                        k1 = rol32(k1, 15);
                        k1 *= c2;
                        _hash ^= k1;
                        break;
                    }
                case 3:
                    {
                        var k1 = (uint)(chunk[offset] | chunk[offset + 1] << 8 | chunk[offset + 2] << 16);
                        k1 *= c1;
                        k1 = rol32(k1, 15);
                        k1 *= c2;
                        _hash ^= k1;
                        break;
                    }
                case 4:
                    {
                        var k1 = (uint)(chunk[offset] | chunk[offset + 1] << 8 | chunk[offset + 2] << 16 | chunk[offset + 3] << 24);
                        k1 *= c1;
                        k1 = rol32(k1, 15);
                        k1 *= c2;
                        _hash ^= k1;
                        _hash = rol32(_hash, 13);
                        _hash = _hash * 5 + 0xE6546B64;
                        break;
                    }
            }
        }

        private static uint rol32(uint x, byte r)
        {
            return (x << r) | (x >> (32 - r));
        }

        private static uint Fmix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85EBCA6B;
            h ^= h >> 13;
            h *= 0xC2B2AE35;
            h ^= h >> 16;
            return h;
        }

        public static new MurMur3 Create() => new();

        public static int HashData(string s) => HashData(Encoding.Unicode.GetBytes(s));

        public static int HashData(byte[] data)
        {
            uint hash = g_seed;
            for (int i = 0; i < data.Length; i += 4)
            {
                var offset = i;
                var length = Math.Min(4, data.Length - offset);
                switch (length)
                {
                    case 1:
                        {
                            var k1 = (uint)data[offset];
                            k1 *= c1;
                            k1 = rol32(k1, 15);
                            k1 *= c2;
                            hash ^= k1;
                            break;
                        }
                    case 2:
                        {
                            var k1 = (uint)(data[offset] | data[offset + 1] << 8);
                            k1 *= c1;
                            k1 = rol32(k1, 15);
                            k1 *= c2;
                            hash ^= k1;
                            break;
                        }
                    case 3:
                        {
                            var k1 = (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16);
                            k1 *= c1;
                            k1 = rol32(k1, 15);
                            k1 *= c2;
                            hash ^= k1;
                            break;
                        }
                    case 4:
                        {
                            var k1 = (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
                            k1 *= c1;
                            k1 = rol32(k1, 15);
                            k1 *= c2;
                            hash ^= k1;
                            hash = rol32(hash, 13);
                            hash = hash * 5 + 0xE6546B64;
                            break;
                        }
                }
            }
            return (int)Fmix(hash ^ (uint)data.Length);
        }
    }
}
