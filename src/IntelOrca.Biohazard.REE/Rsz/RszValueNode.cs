using System;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszValueNode : IRszNode, IEquatable<RszValueNode>
    {
        public RszFieldType Type { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new InvalidOperationException();
        }

        public RszValueNode(RszFieldType type, ReadOnlyMemory<byte> data)
        {
            Type = type;
            Data = data;

            ThrowIfType(RszFieldType.String, nameof(RszStringNode));
            ThrowIfType(RszFieldType.Resource, nameof(RszResourceNode));

            void ThrowIfType(RszFieldType t, string alternative)
            {
                if (type == t)
                    throw new ArgumentException($"Cannot create {nameof(RszValueNode)} for {type}. Use {alternative} instead.", nameof(type));
            }
        }

        internal int AsInt32() => BinaryPrimitives.ReadInt32LittleEndian(Data.Span);

        public bool Equals(RszValueNode other)
        {
            if (Type != other.Type) return false;
            return Data.Span.SequenceEqual(other.Data.Span);
        }

        public override int GetHashCode()
        {
            var span = Data.Span;
            var hash = new HashCode();
            hash.Add(Type);
            foreach (var b in span)
            {
                hash.Add(b);
            }
            return hash.ToHashCode();
        }

        public override bool Equals(object obj) => obj is RszValueNode value && Equals(value);

        public override string ToString() => RszSerializer.Deserialize(this).ToString();
    }
}
