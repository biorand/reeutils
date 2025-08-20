using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public interface IRszSerializable
    {
        object? Deserialize(Type targetClrType);
    }

    public static class RszExtensions
    {
        public static T? Deserialize<T>(this IRszSerializable node)
        {
            return (T?)node.Deserialize(typeof(T));
        }

        public static RszGameObject? FindGameObject(this IRszSceneNode node, Guid guid)
        {
            if (node is RszGameObject gameObject)
            {
                if (gameObject.Guid == guid)
                {
                    return gameObject;
                }
            }

            foreach (var child in node.Children)
            {
                var result = child.FindGameObject(guid);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
