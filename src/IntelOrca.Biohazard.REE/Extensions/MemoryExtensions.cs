using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Extensions
{
    internal static class MemoryExtensions
    {
        public static ReadOnlySpan<T> Get<T>(this ReadOnlyMemory<byte> data, ulong offset, uint count) where T : struct
        {
            return Get<T>(data, (int)offset, (int)count);
        }

        public static ReadOnlySpan<T> Get<T>(this ReadOnlyMemory<byte> data, int offset, int count) where T : struct
        {
            return MemoryMarshal.Cast<byte, T>(data.Span.Slice((int)offset)).Slice(0, (int)count);
        }
    }
}
