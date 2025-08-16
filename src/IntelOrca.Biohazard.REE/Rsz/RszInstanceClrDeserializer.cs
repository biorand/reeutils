using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// Deserialize one or more RSZ instances to CLR objects.
    /// </summary>
    internal class RszInstanceClrDeserializer
    {
        private ImmutableArray<RszInstance> _instanceList = [];
        private ImmutableArray<object>.Builder _objectList = ImmutableArray.CreateBuilder<object>();

        public ImmutableArray<object> Deserialize(ImmutableArray<RszInstance> list)
        {
            _instanceList = list;
            _objectList.Count = _instanceList.Length;
            for (var i = 0; i < _instanceList.Length; i++)
            {
                GetOrCreateClrInstance(new RszInstanceId(i));
            }
            return _objectList.ToImmutable();
        }

        private object? GetOrCreateClrInstance(RszInstanceOrReference instanceOrReference)
        {
            return instanceOrReference.IsReference
                ? GetOrCreateClrInstance(instanceOrReference.AsReference())
                : CreateClrInstance(instanceOrReference.AsInstance());
        }

        private object? GetOrCreateClrInstance(RszInstanceId id)
        {
            var index = id.Index;
            if (index == 0)
                return null;

            var result = _objectList[index];
            if (result != null)
                return result;

            return CreateClrInstance(_instanceList[index]);
        }

        private object? CreateClrInstance(RszInstance instance)
        {
            var rszType = instance.Type;
            var clrType = rszType.ClrType ?? throw new Exception($"No CLR type for {rszType.Name}.");
            switch (rszType.Kind)
            {
                case RszTypeKind.Array:
                {
                    if (instance.Value is RszInstanceOrReference[] list)
                    {
                        var arrayLength = list.Length;
                        var clrInstance = (Array)Activator.CreateInstance(clrType, arrayLength);
                        for (var i = 0; i < arrayLength; i++)
                        {
                            var elementValue = GetOrCreateClrInstance(list[i]);
                            clrInstance.SetValue(elementValue, i);
                        }
                        return clrInstance;
                    }
                    throw new Exception("Invalid value for array instance.");
                }
                case RszTypeKind.Struct:
                {
                    var clrInstance = Activator.CreateInstance(clrType);
                    if (instance.Id.Index != 0)
                    {
                        _objectList[instance.Id.Index] = clrInstance;
                    }

                    if (instance.Value is RszInstanceOrReference[] list)
                    {
                        for (var i = 0; i < rszType.Fields.Length; i++)
                        {
                            var fieldValue = GetOrCreateClrInstance(list[i]);
                            var field = rszType.Fields[i];
                            var clrField = clrType.GetField(field.Name);
                            clrField.SetValue(clrInstance, fieldValue);
                        }
                    }
                    return clrInstance;
                }
                case RszTypeKind.Enum:
                {
                    if (instance.Value is RszInstanceOrReference[] list)
                    {
                        var fieldValue = GetOrCreateClrInstance(list[0]);
                        return Enum.ToObject(clrType, fieldValue);
                    }
                    else
                    {
                        throw new Exception("Invalid value for enum instance.");
                    }
                }
                default:
                {
                    return instance.Value;
                }
            }
        }
    }
}
