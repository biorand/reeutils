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
}
