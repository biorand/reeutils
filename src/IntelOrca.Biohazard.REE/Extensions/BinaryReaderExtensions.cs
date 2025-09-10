using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Extensions
{
    internal unsafe static class BinaryReaderExtensions
    {
        public static T Read<T>(this BinaryReader br) where T : struct
        {
            var result = default(T);
            br.Read(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref result, 1)));
            return result;
        }

        public static T[] ReadArray<T>(this BinaryReader br, int count) where T : struct
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            return MemoryMarshal.Cast<byte, T>(new Span<byte>(br.ReadBytes(count * sizeof(T)))).ToArray();
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        }
    }
}
