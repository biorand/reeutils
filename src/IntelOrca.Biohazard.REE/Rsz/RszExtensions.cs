using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public static class RszExtensions
    {
        public static IEnumerable<RszGameObject> EnumerateGameObjects(this IRszSceneNode node)
        {
            if (node is RszGameObject gameObject)
            {
                yield return gameObject;
            }

            foreach (var child in node.Children)
            {
                foreach (var childGameObject in EnumerateGameObjects(child))
                {
                    yield return childGameObject;
                }
            }
        }

        public static RszGameObject? FindGameObject(this IRszSceneNode node, Func<RszGameObject, bool> condition)
        {
            if (node is RszGameObject gameObject)
            {
                if (condition(gameObject))
                {
                    return gameObject;
                }
            }

            foreach (var child in node.Children)
            {
                var result = child.FindGameObject(condition);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public static RszGameObject? FindGameObject(this IRszSceneNode node, Guid guid) => FindGameObject(node, go => go.Guid == guid);
        public static RszGameObject? FindGameObject(this IRszSceneNode node, string name) => FindGameObject(node, go => go.Name == name);

        public static T ReplaceGameObject<T>(this T node, RszGameObject newGameObject) where T : IRszSceneNode
        {
            if (node.Children.IsDefaultOrEmpty)
                return node;

            if (node is RszGameObject gameObject)
            {
                var children = gameObject.Children.ToBuilder();
                for (var i = 0; i < node.Children.Length; i++)
                {
                    if (children[i].Guid == newGameObject.Guid)
                    {
                        children[i] = newGameObject;
                    }
                    else
                    {
                        children[i] = children[i].ReplaceGameObject(newGameObject);
                    }
                }
                return (T)(IRszSceneNode)gameObject.WithChildren(children.ToImmutable());
            }
            else
            {
                var children = node.Children.ToBuilder();
                for (var i = 0; i < node.Children.Length; i++)
                {
                    if (children[i] is RszGameObject oldGameObject && oldGameObject.Guid == newGameObject.Guid)
                    {
                        children[i] = newGameObject;
                    }
                    else
                    {
                        children[i] = children[i].ReplaceGameObject(newGameObject);
                    }
                }
                return (T)node.WithChildren(children.ToImmutable());
            }
        }

        public static T RemoveGameObject<T>(this T node, Guid guid) where T : IRszSceneNode
        {
            if (node.Children.IsDefaultOrEmpty)
                return node;

            if (node is RszGameObject gameObject)
            {
                var children = gameObject.Children.ToBuilder();
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i].Guid == guid)
                    {
                        children.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        children[i] = children[i].RemoveGameObject(guid);
                    }
                }
                return (T)(IRszSceneNode)gameObject.WithChildren(children.ToImmutable());
            }
            else
            {
                var children = node.Children.ToBuilder();
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i] is RszGameObject oldGameObject && oldGameObject.Guid == guid)
                    {
                        children.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        children[i] = children[i].RemoveGameObject(guid);
                    }
                }
                return (T)node.WithChildren(children.ToImmutable());
            }
        }

        public static T Get<T>(this IRszNode node, string path)
        {
            var result = Get(node, path);
            if (result.GetType() == typeof(T))
                return (T)result;

            return (T)RszSerializer.Deserialize(node, typeof(T))!;
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

        public static T Set<T>(this T node, string path, object value) where T : IRszNode
        {
            return (T)Set((IRszNode)node, path, value);
        }

        private static object Set(this IRszNode node, string path, object value)
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
                if (rightSq == -1)
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
