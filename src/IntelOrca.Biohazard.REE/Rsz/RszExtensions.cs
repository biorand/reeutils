using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public static class RszExtensions
    {
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

        public static T UpdateGameObject<T>(this T node, RszGameObject newGameObject) where T : IRszSceneNode
        {
            if (node.Children.IsDefaultOrEmpty)
                return node;

            var children = node.Children.ToBuilder();
            for (var i = 0; i < node.Children.Length; i++)
            {
                if (children[i] is RszGameObject oldGameObject && oldGameObject.Guid == newGameObject.Guid)
                {
                    children[i] = newGameObject;
                }
                else
                {
                    children[i] = children[i].UpdateGameObject(newGameObject);
                }
            }
            return (T)node.WithChildren(children.ToImmutable());
        }

        public static T RemoveGameObject<T>(this T node, Guid guid) where T : IRszSceneNode
        {
            if (node.Children.IsDefaultOrEmpty)
                return node;

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

        public static void Visit<T>(this T node, Action<IRszNode> cb) where T : IRszNodeContainer
        {
            VisitInternal(node, cb);

            static void VisitInternal(IRszNode node, Action<IRszNode> cb)
            {
                cb(node);
                if (node is RszGameObject gameObject)
                {
                    var components = gameObject.Components.ToBuilder();
                    for (var i = 0; i < components.Count; i++)
                    {
                        VisitInternal(components[i], cb);
                    }

                    var children = gameObject.Children.ToBuilder();
                    for (var i = 0; i < children.Count; i++)
                    {
                        VisitInternal(children[i], cb);
                    }
                }
                if (node is IRszNodeContainer container)
                {
                    var children = container.Children.ToBuilder();
                    for (var i = 0; i < children.Count; i++)
                    {
                        VisitInternal(children[i], cb);
                    }
                }
            }
        }

        public static T Visit<T>(this T node, Func<IRszNode, IRszNode> cb) where T : IRszNodeContainer
        {
            return (T)VisitInternal(node, cb);

            static IRszNode VisitInternal(IRszNode node, Func<IRszNode, IRszNode> cb)
            {
                var newNode = cb(node);
                if (newNode is RszGameObject gameObject)
                {
                    var components = gameObject.Components.ToBuilder();
                    for (var i = 0; i < components.Count; i++)
                    {
                        components[i] = (RszObjectNode)VisitInternal(components[i], cb);
                    }

                    var children = gameObject.Children.ToBuilder();
                    for (var i = 0; i < children.Count; i++)
                    {
                        children[i] = (RszGameObject)VisitInternal(children[i], cb);
                    }

                    return gameObject
                        .WithComponents(components.ToImmutable())
                        .WithChildren(children.ToImmutable());
                }
                if (newNode is IRszNodeContainer container)
                {
                    var children = container.Children.ToBuilder();
                    for (var i = 0; i < children.Count; i++)
                    {
                        children[i] = VisitInternal(children[i], cb);
                    }
                    return container.WithChildren(children.ToImmutable());
                }
                else
                {
                    return newNode;
                }
            }
        }

        public static void VisitGameObjects<T>(this T node, Action<RszGameObject> cb) where T : IRszSceneNode
        {
            if (node is RszGameObject gameObject)
            {
                cb(gameObject);
            }
            var children = node.Children.ToBuilder();
            for (var i = 0; i < node.Children.Length; i++)
            {
                VisitGameObjects(children[i], cb);
            }
        }

        public static T VisitGameObjects<T>(this T node, Func<RszGameObject, RszGameObject> cb) where T : IRszSceneNode
        {
            IRszSceneNode newNode = node is RszGameObject gameObject ? cb(gameObject) : node;
            var children = newNode.Children.ToBuilder();
            for (var i = 0; i < newNode.Children.Length; i++)
            {
                children[i] = VisitGameObjects(children[i], cb);
            }
            return (T)newNode.WithChildren(children.ToImmutable());
        }

        public static void VisitComponents<T>(this T sceneNode, Action<RszObjectNode> cb) where T : IRszSceneNode
        {
            sceneNode.VisitGameObjects(go =>
            {
                foreach (var component in go.Components)
                {
                    cb(component);
                }
            });
        }

        public static void VisitComponents<T>(this T sceneNode, Action<RszGameObject, RszObjectNode> cb) where T : IRszSceneNode
        {
            sceneNode.VisitGameObjects(go =>
            {
                foreach (var component in go.Components)
                {
                    cb(go, component);
                }
            });
        }

        public static T VisitComponents<T>(this T sceneNode, Func<RszObjectNode, RszObjectNode> cb) where T : IRszSceneNode
        {
            return sceneNode.VisitGameObjects(go =>
            {
                var builder = go.Components.ToBuilder();
                for (var i = 0; i < builder.Count; i++)
                {
                    builder[i] = cb(builder[i]);
                }
                return go.WithComponents(builder.ToImmutable());
            });
        }

        public static T VisitComponents<T>(this T sceneNode, Func<RszGameObject, RszObjectNode, RszObjectNode> cb) where T : IRszSceneNode
        {
            return sceneNode.VisitGameObjects(go =>
            {
                var builder = go.Components.ToBuilder();
                for (var i = 0; i < builder.Count; i++)
                {
                    builder[i] = cb(go, builder[i]);
                }
                return go.WithComponents(builder.ToImmutable());
            });
        }

        public static T Get<T>(this IRszNode node) => Get<T>(node, "");

        public static T Get<T>(this IRszNode node, string path)
        {
            var result = Get(node, path);
            if (result.GetType() != typeof(T) && result is IRszNode subNode)
                return (T)RszSerializer.Deserialize(subNode, typeof(T))!;
            return (T)result;
        }

        private static object Get(this IRszNode node, string path)
        {
            if (string.IsNullOrEmpty(path))
                return node;

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

            if (node is RszObjectNode objectNode)
            {
                if (string.IsNullOrEmpty(right))
                {
                    return objectNode[left];
                }
                else
                {
                    return Get(objectNode[left], right);
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

        public static T Set<T>(this T node, string path, object? value) where T : IRszNode
        {
            return (T)Set((IRszNode)node, path, value);
        }

        private static object Set(this IRszNode node, string path, object? value)
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

            if (node is RszObjectNode objectNode)
            {
                if (string.IsNullOrEmpty(right))
                {
                    return value is IRszNode valueNode
                        ? objectNode.SetField(left, valueNode)
                        : objectNode.SetField(left, value);
                }
                else
                {
                    return objectNode.SetField(left, Set(objectNode[left], right, value));
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

        /// <summary>
        /// Clones the game object tree, generating new GUIDs for all game objects,
        /// and fixing any references to the old GUID.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static RszGameObject Clone(this RszGameObject rootGameObject)
        {
            var map = new Dictionary<Guid, Guid>();

            // Create new guids for all game objects
            var root = rootGameObject
                .VisitGameObjects(gameObject =>
                {
                    // Change to new guid (keep map of old to new)
                    var newGuid = Guid.NewGuid();
                    map[gameObject.Guid] = newGuid;
                    return gameObject.WithGuid(newGuid);
                });

            // Fix references
            return root.Visit(node =>
            {
                if (node is RszValueNode valueNode && valueNode.Type == RszFieldType.GameObjectRef)
                {
                    var refGuid = RszSerializer.Deserialize<Guid>(valueNode);
                    if (map.TryGetValue(refGuid, out var newGuid))
                    {
                        return RszSerializer.Serialize(RszFieldType.GameObjectRef, newGuid);
                    }
                }
                return node;
            });
        }
    }
}
