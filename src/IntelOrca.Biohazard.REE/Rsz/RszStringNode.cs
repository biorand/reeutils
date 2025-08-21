using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszStringNode : IRszNode
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

        public override string ToString() => Value;
    }
}
