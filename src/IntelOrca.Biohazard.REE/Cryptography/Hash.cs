using System;
using System.Collections.Generic;
using System.Text;

namespace IntelOrca.Biohazard.REE.Cryptography
{
    internal static class Hash
    {
        public static uint rotl32(uint x, int r)
        {
            return ((x << r) | (x >> (32 - r))) & 0xFFFFFFFFU;
        }
        public static uint Fmix(uint h)
        {
            h ^= h >> 16;
            h = (h * 0x85ebca6bU) & 0xFFFFFFFFU;
            h ^= h >> 13;
            h = (h * 0xc2b2ae35U) & 0xFFFFFFFFU;
            h ^= h >> 16;
            return h;
        }
    }
}
