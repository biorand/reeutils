using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal ref struct RszInstanceDeserializer
    {
        public RszTypeRepository Repository { get; }
        public ReadOnlySpan<byte> Data { get; private set; }

        public RszInstanceDeserializer(RszTypeRepository repository, ReadOnlySpan<byte> data)
        {
            Repository = repository;
            Data = data;
        }

        public RszInstance Read(in RszInstanceInfo info)
        {
            var type = Repository.FromId(info.TypeId)
                ?? throw new Exception($"Unable to find type, Id = {info.TypeId}");
            return Read(type);
        }

        public RszInstance Read(in RszType type)
        {
            switch (type.Kind)
            {
                case RszTypeKind.Array:
                {
                    var arrayLength = BinaryPrimitives.ReadInt32LittleEndian(Data);
                    Data = Data.Slice(4);

                    var elementType = type.ElementType!;
                    if (elementType.Kind == RszTypeKind.Struct)
                    {
                        var value = new RszInstanceReference[arrayLength];
                        for (var i = 0; i < arrayLength; i++)
                        {
                            var id = BinaryPrimitives.ReadInt32LittleEndian(Data);
                            Data = Data.Slice(4);
                            value[i] = new RszInstanceReference(id);
                        }
                        return new RszInstance(type, value);
                    }
                    else
                    {
                        var value = new RszInstance[arrayLength];
                        for (var i = 0; i < arrayLength; i++)
                        {
                            value[i] = Read(type.ElementType!);
                        }
                        return new RszInstance(type, value);
                    }
                }
                case RszTypeKind.Enum:
                case RszTypeKind.Struct:
                {
                    var value = new RszInstance[type.Fields.Length];
                    for (var i = 0; i < type.Fields.Length; i++)
                    {
                        value[i] = Read(type.Fields[i].Type);
                    }
                    return new RszInstance(type, value);
                }
                default:
                {
                    var value = (object?)null;
                    if (type.Kind == RszTypeKind.Int32)
                    {
                        value = BinaryPrimitives.ReadInt32LittleEndian(Data);
                    }
                    Data = Data.Slice(type.Size);
                    return new RszInstance(type, value);
                }
            }
        }
    }
}
