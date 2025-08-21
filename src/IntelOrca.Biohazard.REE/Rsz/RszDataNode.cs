using System;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
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

        public object Decode()
        {
            return Type switch
            {
                RszFieldType.S32 => BinaryPrimitives.ReadInt32LittleEndian(Data.Span),
                _ => throw new NotSupportedException()
            };
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
}
