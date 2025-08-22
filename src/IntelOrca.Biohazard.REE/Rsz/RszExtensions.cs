using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public static class RszExtensions
    {
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

        public static T Get<T>(this IRszNode node, string path)
        {
            var result = Get(node, path);
            return (T)result;
        }

        private static object Get(this IRszNode node, string path)
        {
            var cutIndex = path.IndexOfAny(['.', '['], 1);
            var left = path;
            var right = "";
            if (cutIndex != -1)
            {
                left = path[..cutIndex];
                if (path[cutIndex] == '.' || path[cutIndex] == ']')
                    cutIndex++;
                right = path[cutIndex..];
            }

            if (node is RszStructNode structNode)
            {
                if (string.IsNullOrEmpty(right))
                {
                    return structNode[left];
                }
                else
                {
                    return Get(structNode[left], right);
                }
            }
            else if (node is RszArrayNode arrayNode)
            {
                if (left[0] != '[')
                    throw new Exception("Array nodes must be accessed with square brackets.");

                var rightSq = left.IndexOf(']');
                if (rightSq == -1)
                    throw new Exception("No end bracket found");

                var bracketSlice = left.Substring(1, rightSq - 1);
                var index = int.Parse(bracketSlice);

                return Get(arrayNode[index], right);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static IRszNode Set(this IRszNode node, string path, object value)
        {
            var cutIndex = path.IndexOfAny(['.', '['], 1);
            var left = path;
            var right = "";
            if (cutIndex != -1)
            {
                left = path[..cutIndex];
                if (path[cutIndex] == '.')
                    cutIndex++;
                right = path[cutIndex..];
            }

            if (node is RszStructNode structNode)
            {
                if (string.IsNullOrEmpty(right))
                {
                    return value is IRszNode valueNode
                        ? structNode.SetField(left, valueNode)
                        : structNode.SetField(left, value);
                }
                else
                {
                    return structNode.SetField(left, Set(structNode[left], right, value));
                }
            }
            else if (node is RszArrayNode arrayNode)
            {
                if (left[0] != '[')
                    throw new Exception("Array nodes must be accessed with square brackets.");

                var rightSq = left.IndexOf(']');
                if (rightSq != -1)
                    throw new Exception("No end bracket found");

                var bracketSlice = left.Substring(1, rightSq - 1);
                var index = int.Parse(bracketSlice);

                return arrayNode.SetItem(index, Set(arrayNode[index], right, value));
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
