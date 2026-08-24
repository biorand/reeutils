using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var strOffsetByStr = new Dictionary<string, long>();
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                // The game physically writes one copy of the string per reference, but backpatches
                // every reference to point at the FIRST occurrence of that string.
                if (!strOffsetByStr.TryGetValue(e.Str, out var shared))
                {
                    strOffsetByStr[e.Str] = _stream.Position;
                }

                e.StrOffset = _stream.Position;
                foreach (var ch in e.Str)
                {
                    bw.Write((short)ch);
                }
                bw.Write((short)0);
                _entries[i] = e;
            }

            var backupPosition = _stream.Position;
            foreach (var e in _entries)
            {
                _stream.Position = e.RefOffset;
                var strOffset = strOffsetByStr[e.Str];

                if (e.Length == 4)
                    bw.Write((uint)strOffset);
                else if (e.Length == 8)
                    bw.Write(strOffset);
                else
                    throw new Exception("Unexpected reference offset length.");
            }
            _stream.Position = backupPosition;
        }

        [DebuggerDisplay("{Str}")]
        private struct Entry
        {
            public string Str;
            public long RefOffset;
            public long StrOffset;
            public byte Length;
        }
    }
}
