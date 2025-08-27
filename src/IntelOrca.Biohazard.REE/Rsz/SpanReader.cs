using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal ref struct SpanReader
    {
        private int _address;
        private ReadOnlySpan<byte> _data;

        public int Address => _address;

        public SpanReader(ReadOnlySpan<byte> data)
        {
            _address = 0;
            _data = data;
        }

        public void Seek(int size)
        {
            _address += size;
            _data = _data[size..];
        }

        public void Align(int align)
        {
            var mask = align - 1;
            var rem = _address & mask;
            if (rem != 0)
            {
                Seek(align - rem);
            }
        }

        public int ReadInt32()
        {
            var result = BinaryPrimitives.ReadInt32LittleEndian(_data);
            Seek(4);
            return result;
        }

        public string ReadString()
        {
            var length = ReadInt32();
            if (length == 0)
                return "";

            // Assume null terminator
            var wstrSpan = MemoryMarshal.Cast<byte, char>(_data).Slice(0, length - 1);
            var result = new string(wstrSpan);
            Seek(length * 2);
            return result;
        }

        public byte[] ReadBytes(int size)
        {
            var result = new byte[size];
            _data[0..size].CopyTo(result);
            Seek(size);
            return result;
        }
    }
}
