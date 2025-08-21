using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

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
            if (node is RszStructNode structNode)
            {
                var obj = Activator.CreateInstance(targetClrType)!;
                foreach (var property in targetClrType.GetProperties())
                {
                    var propertyClrType = property.PropertyType;
                    var value = structNode[property.Name];
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
            else if (node is RszDataNode dataNode)
            {
                return Convert.ChangeType(dataNode.Decode(), targetClrType);
            }
            else
            {
                throw new NotSupportedException("This node can't be deserialized.");
            }
        }

        public static IRszNode Serialize(RszType type, object obj)
        {
            if (obj is null)
                return new RszNullNode();

            var clrType = obj.GetType();
            var children = ImmutableArray.CreateBuilder<IRszNode>();
            foreach (var field in type.Fields)
            {
                var property = clrType.GetProperty(field.Name);
                var propertyValue = property.GetValue(obj);
                if (field.IsArray)
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
                    children.Add(new RszArrayNode(arrayChildren.ToImmutableArray()));
                }
                else
                {
                    children.Add(Serialize(field.Type, propertyValue));
                }
            }
            return new RszStructNode(type, children.ToImmutable());
        }

        public static IRszNode Serialize(RszFieldType type, object obj)
        {
            return type switch
            {
                RszFieldType.S32 => new RszDataNode(type, BitConverter.GetBytes(Convert.ToInt32(obj))),
                _ => throw new NotSupportedException()
            };
        }
    }
}
