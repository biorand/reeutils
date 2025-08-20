using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszArrayNode : IRszNode, IRszSerializable
    {
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszArrayNode(ImmutableArray<IRszNode> children)
        {
            Children = children;
        }

        public object Deserialize(Type targetClrType)
        {
            var children = Children;
            if (targetClrType.IsGenericType)
            {
                var genericType = targetClrType.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                {
                    var elementType = genericType.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(targetClrType);
                    for (var i = 0; i < children.Length; i++)
                    {
                        var child = children[i];
                        if (child is IRszSerializable serializable)
                        {
                            list.Add(serializable.Deserialize(elementType));
                        }
                        else
                        {
                            throw new Exception("Node not serializable");
                        }
                    }
                    return list;
                }
            }
            throw new NotSupportedException("Unsupport collection type");
        }
    }
}
