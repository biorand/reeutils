using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal ref struct RszDataReader
    {
        private RszTypeRepository _repository;
        private SpanReader _reader;

        public RszDataReader(RszTypeRepository repository, SpanReader reader)
        {
            _repository = repository;
            _reader = reader;
        }

        public RszInstance Read(RszInstanceId id, RszInstanceInfo info)
        {
            var type = _repository.FromId(info.TypeId)
                ?? throw new Exception($"Unable to find type, Id = {info.TypeId}");
            return Read(id, type);
        }

        public RszInstance Read(RszInstanceId id, RszType type)
        {
            switch (type.Kind)
            {
                case RszTypeKind.Array:
                {
                    var arrayLength = _reader.ReadInt32();
                    var value = new RszInstanceOrReference[arrayLength];
                    var elementType = type.ElementType!;
                    if (elementType.Kind == RszTypeKind.Struct)
                    {
                        for (var i = 0; i < arrayLength; i++)
                        {
                            var refId = _reader.ReadInt32();
                            value[i] = new RszInstanceId(refId);
                        }
                    }
                    else
                    {
                        for (var i = 0; i < arrayLength; i++)
                        {
                            value[i] = Read(default, type.ElementType!);
                        }
                    }
                    return new RszInstance(id, type, value);
                }
                case RszTypeKind.Enum:
                case RszTypeKind.Struct:
                {
                    var value = new RszInstanceOrReference[type.Fields.Length];
                    for (var i = 0; i < type.Fields.Length; i++)
                    {
                        var field = type.Fields[i];
                        if (field.Type.Kind == RszTypeKind.Struct)
                        {
                            var refId = _reader.ReadInt32();
                            value[i] = new RszInstanceId(refId);
                        }
                        else
                        {
                            value[i] = Read(default, type.Fields[i].Type);
                        }
                    }
                    return new RszInstance(id, type, value);
                }
                default:
                {
                    var value = (object?)null;
                    if (type.Kind == RszTypeKind.Int32)
                    {
                        value = _reader.ReadInt32();
                    }
                    else
                    {
                        _reader.Seek(type.Size);
                    }
                    return new RszInstance(id, type, value);
                }
            }
        }
    }
}
