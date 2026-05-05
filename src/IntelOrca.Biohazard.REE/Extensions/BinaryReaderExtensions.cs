using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Extensions
{
    internal static class BinaryReaderExtensions
    {
        public static T ByteToType<T>(this BinaryReader reader) where T : struct
        {
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
