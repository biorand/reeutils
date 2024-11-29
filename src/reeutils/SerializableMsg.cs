using System;

namespace IntelOrca.Biohazard.REEUtils
{
    internal class SerializableMsg
    {
        public int Version { get; set; }
        public int[] Languages { get; set; } = [];
        public Entry[] Entries { get; set; } = [];

        public class Entry
        {
            public Guid Guid { get; set; }
            public string Name { get; set; } = "";
            public string[] Values { get; set; } = [];
        }
    }
}
