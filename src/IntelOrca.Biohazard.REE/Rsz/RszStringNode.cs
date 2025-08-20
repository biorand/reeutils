using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszStringNode : IRszNode, IRszSerializable
    {
        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new InvalidOperationException();
        }
        public string Value { get; }

        public RszStringNode(string value)
        {
            Value = value;
        }

        public object Deserialize(Type targetClrType)
        {
            return Convert.ChangeType(Value, targetClrType);
        }

        public override string ToString() => Value;
    }
}
