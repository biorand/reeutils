using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Extensions
{
    internal unsafe static class BinaryWriterExtensions
    {
        public static void Align(this BinaryWriter bw, int alignment, long alignOffset = 0)
        {
            var position = alignOffset + bw.BaseStream.Position;
            var remainder = (int)(position % alignment);
            if (remainder != 0)
            {
                var skip = alignment - remainder;
                bw.WriteZeros(skip);
            }
        }

        public static void WriteZeros(this BinaryWriter bw, int size)
        {
            for (var i = 0; i < size; i++)
            {
                bw.Write((byte)0);
            }
        }

        public static void WriteZeros<T>(this BinaryWriter bw) where T : unmanaged
        {
            WriteZeros<T>(bw, 1);
        }

        public static void WriteZeros<T>(this BinaryWriter bw, int count) where T : unmanaged
        {
            bw.BaseStream.Position += count * sizeof(T);
        }

        public static void Write<T>(this BinaryWriter bw, in T value) where T : unmanaged
        {
            var value2 = value;
            Write2(bw, ref value2);
        }

        private static void Write2<T>(this BinaryWriter bw, ref T value) where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
#if NETSTANDARD
            MemoryMarshal.Write(buffer, ref value);
#else
            MemoryMarshal.Write(buffer, in value);
#endif
            for (var i = 0; i < buffer.Length; i++)
            {
                bw.Write(buffer[i]);
            }
        }

        public static void Write<T>(this BinaryWriter bw, Span<T> values) where T : unmanaged => Write(bw, (ReadOnlySpan<T>)values);
        public static void Write<T>(this BinaryWriter bw, ReadOnlySpan<T> values) where T : unmanaged
        {
            for (var i = 0; i < values.Length; i++)
            {
                bw.Write(in MemoryMarshal.GetReference(values.Slice(i, 1)));
            }
        }
        public static void Write<T>(this BinaryWriter bw, T[] values) where T : unmanaged
        {
            Write(bw, new ReadOnlySpan<T>(values));
        }

        public static void WriteUTF16(this BinaryWriter bw, string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                bw.Write((ushort)value[i]);
            }
            bw.Write((ushort)0);
        }
    }
}
