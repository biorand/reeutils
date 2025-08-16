using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal readonly struct RszInstanceOrReference
    {
        private readonly RszInstance _instance;
        private readonly RszInstanceId _reference;

        public bool IsInstance => _reference.Index == 0;
        public bool IsReference => _reference.Index != 0;

        public RszInstance AsInstance() => IsInstance ? _instance : throw new InvalidOperationException();
        public RszInstanceId AsReference() => IsReference ? _reference : throw new InvalidOperationException();

        public RszInstanceOrReference(RszInstance instance)
        {
            _instance = instance;
        }

        public RszInstanceOrReference(RszInstanceId reference)
        {
            _reference = reference;
        }

        public static implicit operator RszInstanceOrReference(RszInstance instance) => new(instance);
        public static implicit operator RszInstanceOrReference(RszInstanceId reference) => new(reference);
    }

    internal readonly struct RszInstance(RszInstanceId id, RszType type, object? value)
    {
        public RszInstanceId Id => id;
        public RszType Type => type;
        public object? Value => value;

        public override string ToString()
        {
            if (Type.Kind == RszTypeKind.Enum)
            {
                return ((RszInstance[])Value!)[0].ToString();
            }
            else if (Type.Kind == RszTypeKind.Array)
            {
                var elementType = Type.ElementType!.Name;
                var arrayLength = ((Array)Value!).Length;
                return $"{elementType}[{arrayLength}]";
            }
            else if (Type.Kind == RszTypeKind.Struct)
            {
                return Type.Name;
            }
            else
            {
                return Value?.ToString() ?? "null";
            }
        }
    }

    public readonly struct RszInstanceId(int index)
    {
        public int Index => index;

        public override string ToString() => $"${index}";
    }
}
