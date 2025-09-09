using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Messages
{
    [DebuggerDisplay("{Name}")]
    public sealed class Msg
    {
        public Guid Guid { get; set; }
        public int Crc { get; set; }
        public string Name { get; set; } = "";
        public List<MsgValue> Values { get; set; } = [];
        public List<MsgAttributeValue> Attributes { get; set; } = [];

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
            set
            {
                for (int i = 0; i < Values.Count; i++)
                {
                    if (Values[i].Language == languageId)
                    {
                        Values[i] = new MsgValue(languageId, value);
                        return;
                    }
                }
                throw new ArgumentException($"No value found for language {languageId} in message {Name}.");
            }
        }
    }
}
