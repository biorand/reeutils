using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Messages
{
    [DebuggerDisplay("{Name}")]
    public sealed class Msg
    {
        public Guid Guid { get; set; }
        public int Crc { get; set; }
        public string Name { get; set; } = "";
        public ImmutableArray<MsgValue> Values { get; set; }

        public string this[LanguageId languageId]
        {
            get
            {
                foreach (var value in Values)
                {
                    if (value.Language == languageId)
                    {
                        return value.Text;
                    }
                }
                return string.Empty;
            }
        }
    }
}
