using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard.REE
{
    /// <summary>
    /// Tracks positions in a stream where a string offset should be written. Rewinds the stream and writes the string offset
    /// after writing the strings to the end of the stream.
    /// </summary>
    /// <param name="stream"></param>
    internal sealed class StringPoolBuilder(Stream stream)
    {
        private readonly Stream _stream = stream;
        private readonly List<Entry> _entries = [];

        public void WriteStringOffset32(string s) => WriteStringOffset(s, 4);
        public void WriteStringOffset64(string s) => WriteStringOffset(s, 8);

        private void WriteStringOffset(string s, byte length)
        {
            var streamPosition = _stream.Position;
            _entries.Add(new Entry()
            {
                Str = s,
                RefOffset = streamPosition,
                Length = length
            });
            _stream.Position = streamPosition + length;
        }

        public void WriteStrings()
        {
            var bw = new BinaryWriter(_stream);
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                e.StrOffset = _stream.Position;
                _entries[i] = e;

                foreach (var ch in e.Str)
                {
                    bw.Write((short)ch);
                }
                bw.Write((short)0);
            }

            var backupPosition = _stream.Position;
            foreach (var e in _entries)
            {
                _stream.Position = e.RefOffset;
                if (e.Length == 4)
                    bw.Write((uint)e.StrOffset);
                else if (e.Length == 8)
                    bw.Write(e.StrOffset);
                else
                    throw new Exception("Unexpected reference offset length.");
            }
            _stream.Position = backupPosition;
        }

        private struct Entry
        {
            public string Str;
            public long RefOffset;
            public long StrOffset;
            public byte Length;
        }
    }
}
