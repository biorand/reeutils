using System;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszInstance : IRszNode
    {
        public RszInstanceId Id { get; set; }
        public IRszNode? Value { get; set; }

        public ImmutableArray<IRszNode> Children
        {
            get => Value?.Children ?? [];
            set => Value!.Children = value;
        }

        public override string ToString() => $"{Value}[{Id.Index}]";
    }

    public interface IRszNode
    {
        public ImmutableArray<IRszNode> Children { get; set; }
    }

    public readonly struct RszDataNode : IRszNode
    {
        public RszFieldType Type { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new InvalidOperationException();
        }

        public RszDataNode(RszFieldType type, ReadOnlyMemory<byte> data)
        {
            Type = type;
            Data = data;
        }

        public int AsInt32() => BinaryPrimitives.ReadInt32LittleEndian(Data.Span);

        public override string ToString()
        {
            return Data.Length switch
            {
                1 => Data.Span[0].ToString(),
                2 => BinaryPrimitives.ReadInt16LittleEndian(Data.Span).ToString(),
                4 => BinaryPrimitives.ReadInt32LittleEndian(Data.Span).ToString(),
                8 => BinaryPrimitives.ReadInt64LittleEndian(Data.Span).ToString(),
                _ => $"{Data.Length} bytes"
            };
        }
    }

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

    public class RszArrayNode : IRszNode
    {
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszArrayNode(ImmutableArray<IRszNode> children)
        {
            Children = children;
        }
    }

    public class RszStructNode : IRszNode
    {
        public RszType Type { get; }
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszStructNode(RszType type, ImmutableArray<IRszNode> children)
        {
            Type = type;
            Children = children;
        }

        public override string ToString() => Type.Name.ToString();
    }

    public class RszUserDataNode : IRszNode
    {
        public RszType Type { get; }

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new NotSupportedException();
        }

        public RszUserDataNode(RszType type)
        {
            Type = type;
        }
    }
}
