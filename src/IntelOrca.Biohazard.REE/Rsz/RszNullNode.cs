using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszNullNode : IRszNode, IRszSerializable
    {
        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new NotSupportedException();
        }

        public object? Deserialize(Type targetClrType)
        {
            return null;
        }

        public override string ToString() => "NULL";
    }
}
