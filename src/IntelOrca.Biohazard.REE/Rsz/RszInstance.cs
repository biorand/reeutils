using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal readonly struct RszInstance(RszType type, object? value)
    {
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

    public readonly struct RszInstanceReference(int id)
    {
        public int Id => id;

        public override string ToString() => $"${id}";
    }
}
