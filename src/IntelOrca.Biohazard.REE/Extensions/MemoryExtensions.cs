using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelOrca.Biohazard.REE.Extensions
{
    internal static class MemoryExtensions
    {
        public static ReadOnlySpan<T> Get<T>(this ReadOnlyMemory<byte> data, ulong offset, uint count) where T : struct
        {
            return Get<T>(data, (int)offset, (int)count);
        }

        public static ReadOnlySpan<T> Get<T>(this ReadOnlyMemory<byte> data, long offset, int count) where T : struct
        {
            return Get<T>(data, (int)offset, (int)count);
        }

        public static ReadOnlySpan<T> Get<T>(this ReadOnlyMemory<byte> data, int offset, int count) where T : struct
        {
            if (count == 0)
                return [];
            return MemoryMarshal.Cast<byte, T>(data.Span.Slice((int)offset)).Slice(0, (int)count);
        }

        public static string ReadWString(this ReadOnlyMemory<byte> data, int offset)
            => ReadWString(data.Span, offset);

        public static string ReadWString(this ReadOnlySpan<byte> data, int offset)
        {
            int len = 0;
            while (BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset + len, 2)) != 0)
                len += 2;

            return Encoding.Unicode.GetString(data.Slice(offset, len));
        }

        public static List<T> ToList<T>(this ReadOnlySpan<T> data) where T : struct
        {
            return data.ToArray().ToList();
        }
    }
}
