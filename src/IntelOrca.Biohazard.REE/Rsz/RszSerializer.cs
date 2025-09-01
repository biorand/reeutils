using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Rsz.Native;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszSerializer
    {
        public static T? Deserialize<T>(IRszNode node)
        {
            return (T?)Deserialize(node, typeof(T));
        }

        public static object? Deserialize(IRszNode node, Type targetClrType)
        {
            if (node is null)
            {
                return null;
            }
            else if (targetClrType.IsAssignableFrom(node.GetType()))
            {
                return node;
            }
            else if (node is RszObjectNode objectNode)
            {
                var clrType = FindClrType(objectNode.Type, targetClrType);
                var obj = Activator.CreateInstance(clrType)!;
                foreach (var property in clrType.GetProperties())
                {
                    var propertyClrType = property.PropertyType;
                    var value = objectNode[property.Name];
                    property.SetValue(obj, Deserialize(value, propertyClrType));
                }
                return obj;
            }
            else if (node is RszArrayNode arrayNode)
            {
                var children = arrayNode.Children;
                if (targetClrType.IsGenericType)
                {
                    var genericType = targetClrType.GetGenericTypeDefinition();
                    if (genericType == typeof(List<>))
                    {
                        var elementType = targetClrType.GetGenericArguments()[0];
                        var list = (IList)Activator.CreateInstance(targetClrType);
                        for (var i = 0; i < children.Length; i++)
                        {
                            var child = children[i];
                            list.Add(Deserialize(child, elementType));
                        }
                        return list;
                    }
                    else if (genericType == typeof(ImmutableArray<>))
                    {
                        var elementType = targetClrType.GetGenericArguments()[0];
                        var array = (Array)Activator.CreateInstance(elementType.MakeArrayType(), children.Length);
                        for (var i = 0; i < children.Length; i++)
                        {
                            array.SetValue(Deserialize(children[i], elementType), i);
                        }
                        return CreateImmutableArray(array);
                    }
                }
                else if (targetClrType.IsArray)
                {
                    var elementType = targetClrType.GetElementType();
                    var array = (Array)Activator.CreateInstance(targetClrType, children.Length);
                    for (var i = 0; i < children.Length; i++)
                    {
                        array.SetValue(Deserialize(children[i], elementType), i);
                    }
                    return array;
                }
                throw new NotSupportedException("Unsupport collection type");
            }
            else if (node is RszStringNode stringNode)
            {
                return Convert.ChangeType(stringNode.Value, targetClrType);
            }
            else if (node is RszNullNode)
            {
                return null;
            }
            else if (node is RszValueNode valueNode)
            {
                return Deserialize(valueNode);
            }
            else
            {
                throw new NotSupportedException("This node can't be deserialized.");
            }
        }

        public static IRszNode Serialize(RszType type, object? obj)
        {
            if (obj is null)
                return new RszNullNode();

            var clrName = obj.GetType().FullName.Replace('+', '.');
            if (clrName != type.Name)
            {
                var subRszType = type.Repository.FromName(clrName);
                if (subRszType != null)
                    type = subRszType;
            }

            if (obj is IList objList)
            {
                var objArray = ImmutableArray.CreateBuilder<IRszNode>();
                foreach (var objListItem in objList)
                {
                    objArray.Add(Serialize(type, objListItem));
                }
                return new RszArrayNode(RszFieldType.Object, objArray.ToImmutable());
            }

            var clrType = obj.GetType();
            var children = ImmutableArray.CreateBuilder<IRszNode>();
            foreach (var field in type.Fields)
            {
                var property = clrType.GetProperty(field.Name) ?? throw new Exception($"{field.Name} not found on {clrType.FullName}.");
                var propertyValue = property.GetValue(obj);
                if (field.IsArray)
                {
                    if (propertyValue is not RszArrayNode arrayNode)
                    {
                        var arrayChildren = ImmutableArray.CreateBuilder<IRszNode>();
                        var list = (IList)propertyValue;
                        var listCount = list.Count;
                        for (var i = 0; i < listCount; i++)
                        {
                            var listItem = list[i];
                            if (field.Type == RszFieldType.Object)
                            {
                                var objectType = field.ObjectType ?? throw new Exception("Expected field to have an object type");
                                arrayChildren.Add(Serialize(objectType, listItem));
                            }
                            else
                            {
                                arrayChildren.Add(Serialize(field.Type, listItem));
                            }
                        }
                        arrayNode = new RszArrayNode(field.Type, arrayChildren.ToImmutableArray());
                    }
                    children.Add(arrayNode);
                }
                else
                {
                    if (propertyValue is not IRszNode node)
                    {
                        if (field.Type == RszFieldType.Object)
                        {
                            var objectType = field.ObjectType ?? throw new Exception("Expected field to have an object type");
                            if (propertyValue == null)
                                throw new Exception($"{clrType.FullName}.{property.Name} was null.");
                            node = Serialize(objectType, propertyValue);
                        }
                        else
                        {
                            node = Serialize(field.Type, propertyValue);
                        }
                    }
                    children.Add(node);
                }
            }
            return new RszObjectNode(type, children.ToImmutable());
        }

        public static object Deserialize(RszValueNode node)
        {
            return node.Type switch
            {
                RszFieldType.Bool => MemoryMarshal.Read<bool>(node.Data.Span),
                RszFieldType.S8 => MemoryMarshal.Read<sbyte>(node.Data.Span),
                RszFieldType.U8 => MemoryMarshal.Read<byte>(node.Data.Span),
                RszFieldType.S16 => MemoryMarshal.Read<short>(node.Data.Span),
                RszFieldType.U16 => MemoryMarshal.Read<ushort>(node.Data.Span),
                RszFieldType.S32 => MemoryMarshal.Read<int>(node.Data.Span),
                RszFieldType.U32 => MemoryMarshal.Read<uint>(node.Data.Span),
                RszFieldType.S64 => MemoryMarshal.Read<long>(node.Data.Span),
                RszFieldType.U64 => MemoryMarshal.Read<ulong>(node.Data.Span),
                RszFieldType.F32 => MemoryMarshal.Read<float>(node.Data.Span),
                RszFieldType.F64 => MemoryMarshal.Read<double>(node.Data.Span),
                RszFieldType.Vec2 => MemoryMarshal.Read<Vector2>(node.Data.Span),
                RszFieldType.Vec3 => MemoryMarshal.Read<Vector3>(node.Data.Span),
                RszFieldType.Vec4 => MemoryMarshal.Read<Vector4>(node.Data.Span),
                RszFieldType.Quaternion => MemoryMarshal.Read<Quaternion>(node.Data.Span),
                RszFieldType.Guid or RszFieldType.GameObjectRef => MemoryMarshal.Read<Guid>(node.Data.Span),
                RszFieldType.Range => MemoryMarshal.Read<Native.Range>(node.Data.Span),
                RszFieldType.KeyFrame => MemoryMarshal.Read<KeyFrame>(node.Data.Span),
                _ => throw new NotSupportedException()
            };
        }

        public static IRszNode Serialize(RszFieldType type, object? obj)
        {
            if (obj is null)
            {
                if (type == RszFieldType.Object)
                {
                    return new RszNullNode();
                }
                else
                {
                    throw new ArgumentNullException(nameof(obj));
                }
            }

            if (obj is IList list)
            {
                var children = ImmutableArray.CreateBuilder<IRszNode>(list.Count);
                for (var i = 0; i < list.Count; i++)
                {
                    children.Add(Serialize(type, list[i]));
                }
                return new RszArrayNode(type, children.ToImmutable());
            }

            if (obj is RszValueNode valueNode)
            {
                if (valueNode.Type != type)
                {
                    throw new Exception($"Cannot serialize RszValueNode({valueNode.Type}) to {type}.");
                }
                return valueNode;
            }

            return type switch
            {
                RszFieldType.Bool => new RszValueNode(type, ToMemory<bool>(obj)),
                RszFieldType.S8 => new RszValueNode(type, ToMemory<sbyte>(obj)),
                RszFieldType.U8 => new RszValueNode(type, ToMemory<byte>(obj)),
                RszFieldType.S16 => new RszValueNode(type, ToMemory<short>(obj)),
                RszFieldType.U16 => new RszValueNode(type, ToMemory<ushort>(obj)),
                RszFieldType.S32 => new RszValueNode(type, ToMemory<int>(obj)),
                RszFieldType.U32 => new RszValueNode(type, ToMemory<uint>(obj)),
                RszFieldType.S64 => new RszValueNode(type, ToMemory<long>(obj)),
                RszFieldType.U64 => new RszValueNode(type, ToMemory<ulong>(obj)),
                RszFieldType.F32 => new RszValueNode(type, ToMemory<float>(obj)),
                RszFieldType.F64 => new RszValueNode(type, ToMemory<double>(obj)),
                RszFieldType.Vec2 => new RszValueNode(type, ToMemory<Vector2>(obj)),
                RszFieldType.Vec3 => new RszValueNode(type, ToMemory<Vector3>(obj)),
                RszFieldType.Vec4 => new RszValueNode(type, ToMemory<Vector4>(obj)),
                RszFieldType.Quaternion => new RszValueNode(type, ToMemory<Quaternion>(obj)),
                RszFieldType.Guid or RszFieldType.GameObjectRef => new RszValueNode(type, ToMemory<Guid>(obj)),
                RszFieldType.Range => new RszValueNode(type, ToMemory<Native.Range>(obj)),
                RszFieldType.KeyFrame => new RszValueNode(type, ToMemory<KeyFrame>(obj)),
                RszFieldType.String => obj is RszStringNode stringNode
                    ? stringNode
                    : new RszStringNode((string)obj),
                RszFieldType.Resource => obj is RszResourceNode resourceNode
                    ? resourceNode
                    : new RszResourceNode((string)obj),
                RszFieldType.UserData => (RszUserDataNode)obj,
                _ => throw new NotSupportedException()
            };
        }

        private static Type FindClrType(RszType rszType, Type targetClrType)
        {
            if (rszType.Name != targetClrType.FullName.Replace('+', '.'))
            {
                // Look for inheritance
                var foundClrType = targetClrType.Assembly.DefinedTypes.FirstOrDefault(x => x.FullName == rszType.Name);
                if (foundClrType == null)
                    throw new Exception($"Expected to deserialize {targetClrType.FullName} but got {rszType.Name}.");

                if (!foundClrType.IsSubclassOf(targetClrType))
                    throw new Exception($"{foundClrType} is not a sub class of {targetClrType}.");

                return foundClrType;
            }
            return targetClrType;
        }

        private static ReadOnlyMemory<byte> ToMemory<T>(object value) where T : struct
        {
            var result = (T)Convert.ChangeType(value, typeof(T));
            var span = MemoryMarshal.CreateReadOnlySpan(ref result, 1);
            var bytes = MemoryMarshal.Cast<T, byte>(span);
            return new ReadOnlyMemory<byte>(bytes.ToArray());
        }

        public static object CreateImmutableArray(Array items)
        {
            var elementType = items.GetType().GetElementType();
            var createWithArray = typeof(ImmutableArray)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(ImmutableArray.Create) && m.IsGenericMethodDefinition)
                .Select(m => new { Method = m, Params = m.GetParameters() })
                .First(x => x.Params.Length == 1 && x.Params[0].ParameterType.IsArray)
                .Method
                .MakeGenericMethod(elementType);
            return createWithArray.Invoke(null, [items])!;
        }
    }
}
