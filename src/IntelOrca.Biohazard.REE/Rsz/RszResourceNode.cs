using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszResourceNode : IRszNode
    {
        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new InvalidOperationException();
        }
        public string? Value { get; }

        public RszResourceNode(string? value)
        {
            Value = value;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public override string? ToString() => Value;
    }
}
