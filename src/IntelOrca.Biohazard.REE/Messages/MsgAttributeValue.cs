using System;

namespace IntelOrca.Biohazard.REE.Messages
{
    public readonly struct MsgAttributeValue
    {
        public MsgAttributeDefinition Definition { get; }
        public MsgAttributeType Type => Definition.Type;
        public string Name => Definition.Name;
        public object Value { get; }

        public MsgAttributeValue(MsgAttributeDefinition definition, object value)
        {
            // Validate value
            var type = definition.Type;
            if (type == MsgAttributeType.Null && value is not ulong)
                throw new ArgumentException($"Value must be of type ulong for {type} attribute type.", nameof(value));
            if (type == MsgAttributeType.Int64 && value is not long)
                throw new ArgumentException($"Value must be of type long for {type} attribute type.", nameof(value));
            if (type == MsgAttributeType.Wstring && value is not string)
                throw new ArgumentException($"Value must be of type string for {type} attribute type.", nameof(value));
            if (type == MsgAttributeType.Double && value is not double)
                throw new ArgumentException($"Value must be of type double for {type} attribute type.", nameof(value));

            Definition = definition;
            Value = value;
        }

        public override string ToString() => string.IsNullOrEmpty(Name) ? $"Unnamed {Type}" : Name;
    }
}
