using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal ref struct SpanReader
    {
        private ReadOnlySpan<byte> _data;

        public SpanReader(ReadOnlySpan<byte> data)
        {
            _data = data;
        }

        public void Seek(int size)
        {
            _data = _data[size..];
        }

        public int ReadInt32()
        {
            var result = BinaryPrimitives.ReadInt32LittleEndian(_data);
            Seek(4);
            return result;
        }
    }
}
