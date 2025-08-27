using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal ref struct RszDataReader
    {
        private RszTypeRepository _repository;
        private SpanReader _reader;

        public int BytesRead => _reader.Address;

        public RszDataReader(RszTypeRepository repository, SpanReader reader)
        {
            _repository = repository;
            _reader = reader;
        }

        public RszObjectNode ReadStruct(RszType type)
        {
            var fieldValues = ImmutableArray.CreateBuilder<IRszNode>(type.Fields.Length);
            foreach (var f in type.Fields)
            {
                fieldValues.Add(ReadField(f));
            }
            return new RszObjectNode(type, fieldValues.ToImmutable());
        }

        public IRszNode ReadField(RszTypeField field)
        {
            if (field.IsArray)
            {
                _reader.Align(4);
                var arrayLength = _reader.ReadInt32();
                var array = ImmutableArray.CreateBuilder<IRszNode>(arrayLength);
                if (arrayLength > 0)
                {
                    _reader.Align(field.Align);
                }
                for (var i = 0; i < arrayLength; i++)
                {
                    array.Add(ReadValue(field));
                }
                return new RszArrayNode(field.Type, array.ToImmutable());
            }
            else
            {
                _reader.Align(field.Align);
                return ReadValue(field);
            }
        }

        public IRszNode ReadValue(RszTypeField field)
        {
            if (field.Type == RszFieldType.String)
            {
                _reader.Align(4);
                var value = _reader.ReadString();
                return new RszStringNode(value);
            }
            else if (field.Type == RszFieldType.Resource)
            {
                _reader.Align(4);
                var value = _reader.ReadString();
                return new RszResourceNode(value);
            }
            else
            {
                var data = _reader.ReadBytes(field.Size);
                return new RszValueNode(field.Type, data);
            }
        }
    }
}
