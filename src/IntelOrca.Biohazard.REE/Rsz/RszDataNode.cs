using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Rsz.Native;

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
                RszFieldType.Bool => MemoryMarshal.Read<bool>(Data.Span),
                RszFieldType.S8 => MemoryMarshal.Read<sbyte>(Data.Span),
                RszFieldType.U8 => MemoryMarshal.Read<byte>(Data.Span),
                RszFieldType.S16 => MemoryMarshal.Read<short>(Data.Span),
                RszFieldType.U16 => MemoryMarshal.Read<ushort>(Data.Span),
                RszFieldType.S32 => MemoryMarshal.Read<int>(Data.Span),
                RszFieldType.U32 => MemoryMarshal.Read<uint>(Data.Span),
                RszFieldType.S64 => MemoryMarshal.Read<long>(Data.Span),
                RszFieldType.U64 => MemoryMarshal.Read<ulong>(Data.Span),
                RszFieldType.F32 => MemoryMarshal.Read<float>(Data.Span),
                RszFieldType.F64 => MemoryMarshal.Read<double>(Data.Span),
                RszFieldType.Vec2 => MemoryMarshal.Read<Vector2>(Data.Span),
                RszFieldType.Vec3 => MemoryMarshal.Read<Vector3>(Data.Span),
                RszFieldType.Vec4 => MemoryMarshal.Read<Vector4>(Data.Span),
                RszFieldType.Quaternion => MemoryMarshal.Read<Quaternion>(Data.Span),
                RszFieldType.Guid or RszFieldType.GameObjectRef => MemoryMarshal.Read<Guid>(Data.Span),
                RszFieldType.Range => MemoryMarshal.Read<Native.Range>(Data.Span),
                RszFieldType.KeyFrame => MemoryMarshal.Read<KeyFrame>(Data.Span),
                _ => throw new NotSupportedException()
            };
        }

        public int AsInt32() => BinaryPrimitives.ReadInt32LittleEndian(Data.Span);

        public override string ToString() => Decode().ToString();
    }
}
