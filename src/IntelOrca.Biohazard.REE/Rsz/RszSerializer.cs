using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

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
                var obj = CreateClrInstance<object>(clrType)!;
                foreach (var property in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
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
                        var list = CreateClrInstance<IList>(targetClrType)!;
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
                        var array = CreateClrInstance<Array>(elementType.MakeArrayType(), children.Length);
                        for (var i = 0; i < children.Length; i++)
                        {
                            array.SetValue(Deserialize(children[i], elementType), i);
                        }
                        return CreateImmutableArray(array);
                    }
                }
                else if (targetClrType.IsArray)
                {
                    var elementType = targetClrType.GetElementType()!;
                    var array = CreateClrInstance<Array>(targetClrType, children.Length)!;
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

            static T CreateClrInstance<T>(Type type, params object[] args)
            {
                return (T)(Activator.CreateInstance(type, args) ?? throw new Exception($"Failed to create instance of {type}."));
            }
        }

        public static IRszNode Serialize(RszType type, object? obj)
        {
            if (obj is null)
                return new RszNullNode();

            var clrName = obj.GetType().FullName!.Replace('+', '.');
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
                        var list = (IList)propertyValue!;
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
                                arrayChildren.Add(Serialize(field.Type, listItem, type.Repository));
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
                            node = Serialize(field.Type, propertyValue, type.Repository);
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
                RszFieldType.Mat4 => MemoryMarshal.Read<Matrix4x4>(node.Data.Span),
                RszFieldType.Quaternion => MemoryMarshal.Read<Quaternion>(node.Data.Span),
                RszFieldType.Guid or RszFieldType.GameObjectRef => MemoryMarshal.Read<Guid>(node.Data.Span),
                RszFieldType.Uint2 => MemoryMarshal.Read<via.Uint2>(node.Data.Span),
                RszFieldType.Uint3 => MemoryMarshal.Read<via.Uint3>(node.Data.Span),
                RszFieldType.Uint4 => MemoryMarshal.Read<via.Uint4>(node.Data.Span),
                RszFieldType.Int2 => MemoryMarshal.Read<via.Int2>(node.Data.Span),
                RszFieldType.Int3 => MemoryMarshal.Read<via.Int3>(node.Data.Span),
                RszFieldType.Int4 => MemoryMarshal.Read<via.Int4>(node.Data.Span),
                RszFieldType.Color => MemoryMarshal.Read<via.Color>(node.Data.Span),
                RszFieldType.AABB => MemoryMarshal.Read<via.AABB>(node.Data.Span),
                RszFieldType.Capsule => MemoryMarshal.Read<via.Capsule>(node.Data.Span),
                RszFieldType.TaperedCapsule => MemoryMarshal.Read<via.TaperedCapsule>(node.Data.Span),
                RszFieldType.Cone => MemoryMarshal.Read<via.Cone>(node.Data.Span),
                RszFieldType.Line => MemoryMarshal.Read<via.Line>(node.Data.Span),
                RszFieldType.LineSegment => MemoryMarshal.Read<via.LineSegment>(node.Data.Span),
                RszFieldType.OBB => MemoryMarshal.Read<via.OBB>(node.Data.Span),
                RszFieldType.Plane => MemoryMarshal.Read<via.Plane>(node.Data.Span),
                RszFieldType.PlaneXZ => MemoryMarshal.Read<via.PlaneXZ>(node.Data.Span),
                RszFieldType.Point => MemoryMarshal.Read<via.Point>(node.Data.Span),
                RszFieldType.Range => MemoryMarshal.Read<via.Range>(node.Data.Span),
                RszFieldType.RangeI => MemoryMarshal.Read<via.RangeI>(node.Data.Span),
                RszFieldType.Ray => MemoryMarshal.Read<via.Ray>(node.Data.Span),
                RszFieldType.RayY => MemoryMarshal.Read<via.RayY>(node.Data.Span),
                RszFieldType.Segment => MemoryMarshal.Read<via.Segment>(node.Data.Span),
                RszFieldType.Size => MemoryMarshal.Read<via.Size>(node.Data.Span),
                RszFieldType.Sphere => MemoryMarshal.Read<via.Sphere>(node.Data.Span),
                RszFieldType.Triangle => MemoryMarshal.Read<via.Triangle>(node.Data.Span),
                RszFieldType.Cylinder => MemoryMarshal.Read<via.Cylinder>(node.Data.Span),
                RszFieldType.Ellipsoid => MemoryMarshal.Read<via.Ellipsoid>(node.Data.Span),
                RszFieldType.Area => MemoryMarshal.Read<via.Area>(node.Data.Span),
                RszFieldType.Torus => MemoryMarshal.Read<via.Torus>(node.Data.Span),
                RszFieldType.Rect => MemoryMarshal.Read<via.Rect>(node.Data.Span),
                RszFieldType.Rect3D => MemoryMarshal.Read<via.Rect3D>(node.Data.Span),
                RszFieldType.Frustum => MemoryMarshal.Read<via.Frustum>(node.Data.Span),
                RszFieldType.KeyFrame => MemoryMarshal.Read<via.KeyFrame>(node.Data.Span),
                RszFieldType.Sfix => MemoryMarshal.Read<via.sfix>(node.Data.Span),
                RszFieldType.Sfix2 => MemoryMarshal.Read<via.Sfix2>(node.Data.Span),
                RszFieldType.Sfix3 => MemoryMarshal.Read<via.Sfix3>(node.Data.Span),
                RszFieldType.Sfix4 => MemoryMarshal.Read<via.Sfix4>(node.Data.Span),
                RszFieldType.Position => MemoryMarshal.Read<via.Position>(node.Data.Span),
                _ => node.Data
            };
        }

        public static IRszNode Serialize(RszFieldType type, object? obj, RszTypeRepository? typeRepository = null)
        {
            if (obj is null)
            {
                return type switch
                {
                    RszFieldType.Object => new RszNullNode(),
                    RszFieldType.String or RszFieldType.RuntimeType => new RszStringNode(),
                    RszFieldType.Resource => new RszResourceNode(),
                    RszFieldType.UserData => new RszUserDataNode(),
                    _ => throw new ArgumentNullException(nameof(obj))
                };
            }

            if (obj is IList list)
            {
                var children = ImmutableArray.CreateBuilder<IRszNode>(list.Count);
                for (var i = 0; i < list.Count; i++)
                {
                    children.Add(Serialize(type, list[i], typeRepository));
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
                RszFieldType.Mat4 => new RszValueNode(type, ToMemory<Matrix4x4>(obj)),
                RszFieldType.Quaternion => new RszValueNode(type, ToMemory<Quaternion>(obj)),
                RszFieldType.Guid or RszFieldType.GameObjectRef => new RszValueNode(type, ToMemory<Guid>(obj)),
                RszFieldType.Uint2 => new RszValueNode(type, ToMemory<via.Uint2>(obj)),
                RszFieldType.Uint3 => new RszValueNode(type, ToMemory<via.Uint3>(obj)),
                RszFieldType.Uint4 => new RszValueNode(type, ToMemory<via.Uint4>(obj)),
                RszFieldType.Int2 => new RszValueNode(type, ToMemory<via.Int2>(obj)),
                RszFieldType.Int3 => new RszValueNode(type, ToMemory<via.Int3>(obj)),
                RszFieldType.Int4 => new RszValueNode(type, ToMemory<via.Int4>(obj)),
                RszFieldType.Color => new RszValueNode(type, ToMemory<via.Color>(obj)),
                RszFieldType.AABB => new RszValueNode(type, ToMemory<via.AABB>(obj)),
                RszFieldType.Capsule => new RszValueNode(type, ToMemory<via.Capsule>(obj)),
                RszFieldType.TaperedCapsule => new RszValueNode(type, ToMemory<via.TaperedCapsule>(obj)),
                RszFieldType.Cone => new RszValueNode(type, ToMemory<via.Cone>(obj)),
                RszFieldType.Line => new RszValueNode(type, ToMemory<via.Line>(obj)),
                RszFieldType.LineSegment => new RszValueNode(type, ToMemory<via.LineSegment>(obj)),
                RszFieldType.OBB => new RszValueNode(type, ToMemory<via.OBB>(obj)),
                RszFieldType.Plane => new RszValueNode(type, ToMemory<via.Plane>(obj)),
                RszFieldType.PlaneXZ => new RszValueNode(type, ToMemory<via.PlaneXZ>(obj)),
                RszFieldType.Point => new RszValueNode(type, ToMemory<via.Point>(obj)),
                RszFieldType.Range => new RszValueNode(type, ToMemory<via.Range>(obj)),
                RszFieldType.RangeI => new RszValueNode(type, ToMemory<via.RangeI>(obj)),
                RszFieldType.Ray => new RszValueNode(type, ToMemory<via.Ray>(obj)),
                RszFieldType.RayY => new RszValueNode(type, ToMemory<via.RayY>(obj)),
                RszFieldType.Segment => new RszValueNode(type, ToMemory<via.Segment>(obj)),
                RszFieldType.Size => new RszValueNode(type, ToMemory<via.Size>(obj)),
                RszFieldType.Sphere => new RszValueNode(type, ToMemory<via.Sphere>(obj)),
                RszFieldType.Triangle => new RszValueNode(type, ToMemory<via.Triangle>(obj)),
                RszFieldType.Cylinder => new RszValueNode(type, ToMemory<via.Cylinder>(obj)),
                RszFieldType.Ellipsoid => new RszValueNode(type, ToMemory<via.Ellipsoid>(obj)),
                RszFieldType.Area => new RszValueNode(type, ToMemory<via.Area>(obj)),
                RszFieldType.Torus => new RszValueNode(type, ToMemory<via.Torus>(obj)),
                RszFieldType.Rect => new RszValueNode(type, ToMemory<via.Rect>(obj)),
                RszFieldType.Rect3D => new RszValueNode(type, ToMemory<via.Rect3D>(obj)),
                RszFieldType.Frustum => new RszValueNode(type, ToMemory<via.Frustum>(obj)),
                RszFieldType.KeyFrame => new RszValueNode(type, ToMemory<via.KeyFrame>(obj)),
                RszFieldType.Sfix => new RszValueNode(type, ToMemory<via.sfix>(obj)),
                RszFieldType.Sfix2 => new RszValueNode(type, ToMemory<via.Sfix2>(obj)),
                RszFieldType.Sfix3 => new RszValueNode(type, ToMemory<via.Sfix3>(obj)),
                RszFieldType.Sfix4 => new RszValueNode(type, ToMemory<via.Sfix4>(obj)),
                RszFieldType.Position => new RszValueNode(type, ToMemory<via.Position>(obj)),
                RszFieldType.String or RszFieldType.RuntimeType => obj is RszStringNode stringNode
                    ? stringNode
                    : new RszStringNode((string)obj),
                RszFieldType.Resource => obj is RszResourceNode resourceNode
                    ? resourceNode
                    : new RszResourceNode((string)obj),
                RszFieldType.UserData => (RszUserDataNode)obj,
                RszFieldType.Object => typeRepository == null
                    ? throw new ArgumentException("Unable to serialize objects without a repository")
                    : Serialize(typeRepository.FromName(obj.GetType().FullName ?? "") ?? throw new ArgumentException($"{obj.GetType().FullName} not found in repository."), obj),
                _ => throw new NotSupportedException()
            };
        }

        private static Type FindClrType(RszType rszType, Type targetClrType)
        {
            if (rszType.Name != targetClrType.FullName!.Replace('+', '.'))
            {
                // Look for inheritance
                var foundClrType = targetClrType.Assembly.DefinedTypes.FirstOrDefault(x => x.FullName?.Replace('+', '.') == rszType.Name);
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
            var elementType = items.GetType().GetElementType()!;
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
