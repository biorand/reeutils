using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using RszTool;

namespace IntelOrca.Biohazard.REEUtils
{
    internal sealed class RszInstanceSerializer(RSZFile rsz)
    {
        public string Serialize(UserFile usr, JsonSerializerOptions? options = null) => Serialize(usr.RSZ!.ObjectList[0], options);

        private string Serialize(RszInstance instance, JsonSerializerOptions? options = null)
        {
            var dict = ToDictionary(instance);
            return JsonSerializer.Serialize(dict, options ?? new JsonSerializerOptions()
            {
                IncludeFields = true,
                WriteIndented = true
            });
        }

        private static Dictionary<string, object> ToDictionary(RszInstance instance)
        {
            var dict = new Dictionary<string, object>();
            dict["$type"] = instance.RszClass.name;
            for (var i = 0; i < instance.Fields.Length; i++)
            {
                var field = instance.Fields[i];
                if (instance.Values.Length <= i)
                    continue;

                var value = instance.Values[i];
                if (value is RszInstance child)
                {
                    if (child.RSZUserData is RSZUserDataInfo userDataInfo)
                    {
                        var d = new Dictionary<string, object>();
                        d["$type"] = child.RszClass.name;
                        d["$path"] = userDataInfo.Path!;
                        value = d;
                    }
                    else
                    {
                        value = ToDictionary(child);
                    }
                }
                else if (value is List<object> list)
                {
                    var copy = list.ToList();
                    for (var j = 0; j < copy.Count; j++)
                    {
                        if (copy[j] is RszInstance el)
                        {
                            copy[j] = ToDictionary(el);
                        }
                    }
                    value = copy;
                }
                dict[field.name] = value;
            }
            return dict;
        }

        public string Serialize(ScnFile scn, JsonSerializerOptions? options = null)
        {
            var folders = scn.FolderDatas?.Select(SerializeFolder).ToArray() ?? [];
            var unfoldered = scn.GameObjectDatas?.Select(SerializeGameObject).ToArray() ?? [];
            return JsonSerializer.Serialize(folders.Concat(unfoldered).ToArray(), options);
        }

        private object SerializeFolder(ScnFile.FolderData folder)
        {
            var dict = ToDictionary(folder.Instance!);
            dict["children"] = folder.Children.Select(SerializeFolder)
                .Concat(folder.GameObjects.Select(SerializeGameObject).ToArray())
                .ToArray();
            return dict;
        }

        private object SerializeGameObject(ScnFile.GameObjectData gameObject)
        {
            var dict = ToDictionary(gameObject.Instance!);
            dict["guid"] = gameObject.Guid;
            if (gameObject.Prefab?.Path is string prefab)
                dict["prefab"] = prefab;
            dict["components"] = gameObject.Components.Select(ToDictionary).ToArray();
            dict["children"] = gameObject.Children.Select(SerializeGameObject).ToArray();
            return dict;
        }

        public RszInstance DeserializeUserFile(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new Exception("Root must be an object");

            return DeserializeObject(el);
        }

        public void DeserializeScnFile(ScnFile scn, JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array)
                throw new Exception("Root must be an array");

            var children = DeserializeChildren(el);
            scn.FolderDatas = [.. children.OfType<ScnFile.FolderData>()];
            scn.GameObjectDatas = [.. children.OfType<ScnFile.GameObjectData>()];
        }

        private object[] DeserializeChildren(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array)
                throw new Exception("Children must be an array");

            var result = new List<object>();
            foreach (var element in el.EnumerateArray())
            {
                var type = element.GetStringProperty("$type");
                if (type == "via.Folder")
                {
                    var folder = new ScnFile.FolderData();
                    folder.Info = new StructModel<ScnFile.FolderInfo>();
                    folder.Instance = DeserializeObject(element);
                    if (element.TryGetProperty("children", out var jChildren))
                    {
                        var grandchildren = DeserializeChildren(jChildren);
                        foreach (var gc in grandchildren)
                        {
                            if (gc is ScnFile.FolderData folderChild)
                            {
                                folderChild.Parent = folder;
                                folder.Children.Add(folderChild);
                            }
                            else if (gc is ScnFile.GameObjectData gameDataChild)
                            {
                                gameDataChild.Folder = folder;
                                folder.GameObjects.Add(gameDataChild);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                    }
                    result.Add(folder);
                }
                else if (type == "via.GameObject")
                {
                    var gameObject = new ScnFile.GameObjectData();
                    gameObject.Info = new StructModel<ScnFile.GameObjectInfo>();
                    gameObject.Instance = DeserializeObject(element);
                    gameObject.Guid = element.TryGetProperty("guid", out var jGuid)
                        ? jGuid.GetGuid()
                        : Guid.NewGuid();
                    if (element.TryGetProperty("prefab", out var jPrefab))
                    {
                        gameObject.Prefab = new ScnFile.PrefabInfo()
                        {
                            Path = jPrefab.GetString()
                        };
                    }
                    if (element.TryGetProperty("components", out var jComponents))
                    {
                        foreach (var jComponent in jComponents.EnumerateArray())
                        {
                            gameObject.Components.Add(DeserializeObject(jComponent));
                        }
                    }
                    if (element.TryGetProperty("children", out var jChildren))
                    {
                        var grandchildren = DeserializeChildren(jChildren);
                        foreach (var gc in grandchildren)
                        {
                            if (gc is ScnFile.GameObjectData gameDataChild)
                            {
                                gameDataChild.Parent = gameObject;
                                gameObject.Children.Add(gameDataChild);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                    }
                    result.Add(gameObject);
                }
                else
                {
                    throw new NotSupportedException($"{type} not supported. Only via.Folder and via.GameObject.");
                }
            }
            return result.ToArray();
        }

        private RszInstance DeserializeObject(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new Exception("Expected object");

            var type = el.GetStringProperty("$type")!;
            if (el.TryGetProperty("$path", out var jPath))
            {
                var externalPath = jPath.GetString();
                var rszClass = rsz.RszParser.GetRSZClass(type) ?? throw new Exception($"{type} not found");
                return new RszInstance(rszClass, userData: new RSZUserDataInfo()
                {
                    Path = externalPath
                });
            }
            else
            {
                var result = rsz.CreateInstance(type);
                foreach (var f in result.Fields)
                {
                    if (el.TryGetProperty(f.name, out var propEl))
                    {
                        result.SetFieldValue(f.name, DeserializeField(f, propEl));
                    }
                }
                return result;
            }
        }

        private object DeserializeField(RszField field, JsonElement el)
        {
            return field.array
                ? DeserializeArray(field, el)
                : DeserializeElement(field, el);
        }

        private object DeserializeArray(RszField field, JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array)
                throw new Exception("Expected array");

            var list = new List<object>();
            foreach (var jArrayItem in el.EnumerateArray())
            {
                list.Add(DeserializeElement(field, jArrayItem));
            }
            return list;
        }

        private object DeserializeElement(RszField field, JsonElement el)
        {
            try
            {
                return field.type switch
                {
                    RszFieldType.Resource => el.GetString()!,
                    RszFieldType.UserData => DeserializeObject(el),
                    RszFieldType.Bool => el.GetBoolean(),
                    RszFieldType.String => el.GetString()!,
                    RszFieldType.S8 => el.GetSByte(),
                    RszFieldType.U8 => el.GetByte(),
                    RszFieldType.S16 => el.GetInt16(),
                    RszFieldType.U16 => el.GetUInt16(),
                    RszFieldType.S32 => el.GetInt32(),
                    RszFieldType.U32 => el.GetUInt32(),
                    RszFieldType.F32 => el.GetSingle(),
                    RszFieldType.Object => DeserializeObject(el),
                    RszFieldType.Vec2 => new Vector2(
                        el.GetProperty("X").GetSingle(),
                        el.GetProperty("Y").GetSingle()),
                    RszFieldType.Vec3 => new Vector3(
                        el.GetProperty("X").GetSingle(),
                        el.GetProperty("Y").GetSingle(),
                        el.GetProperty("Z").GetSingle()),
                    RszFieldType.Vec4 => new Vector4(
                        el.GetProperty("X").GetSingle(),
                        el.GetProperty("Y").GetSingle(),
                        el.GetProperty("Z").GetSingle(),
                        el.GetProperty("W").GetSingle()),
                    RszFieldType.Quaternion => new Quaternion(
                        el.GetProperty("X").GetSingle(),
                        el.GetProperty("Y").GetSingle(),
                        el.GetProperty("Z").GetSingle(),
                        el.GetProperty("W").GetSingle()),
                    RszFieldType.Guid => Guid.Parse(el.GetString()!),
                    RszFieldType.OBB => new RszTool.via.OBB()
                    {
                        Coord = ParseMatrix4(el.GetProperty("Coord")),
                        Extent = ParseVector3(el.GetProperty("Extent"))
                    },
                    RszFieldType.Range => new RszTool.via.Range()
                    {
                        r = el.GetProperty("r").GetSingle(),
                        s = el.GetProperty("s").GetSingle()
                    },
                    RszFieldType.Size => new RszTool.via.Size()
                    {
                        w = el.GetProperty("w").GetSingle(),
                        h = el.GetProperty("h").GetSingle()
                    },
                    RszFieldType.GameObjectRef => Guid.Parse(el.GetString()!),
                    RszFieldType.Data => el.ValueKind switch
                    {
                        // JsonValueKind.Number => el.GetInt32(),
                        // JsonValueKind.Object => DeserializeObject(el),
                        JsonValueKind.String => el.GetBytesFromBase64(),
                        _ => throw new NotImplementedException("Unknown data field")
                    },
                    _ => throw new NotImplementedException($"Deserialization of {field.type} is not implemented")
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Issue with setting field {field.name} to {el}. {ex.Message}", ex);
            }
        }

        private static Vector3 ParseVector3(JsonElement el)
        {
            return new Vector3(
                el.GetProperty("X").GetSingle(),
                el.GetProperty("Y").GetSingle(),
                el.GetProperty("Z").GetSingle());
        }

        private static RszTool.via.mat4 ParseMatrix4(JsonElement el)
        {
            var result = new RszTool.via.mat4();
            result.m00 = el.GetProperty("m00").GetSingle();
            result.m01 = el.GetProperty("m01").GetSingle();
            result.m02 = el.GetProperty("m02").GetSingle();
            result.m03 = el.GetProperty("m03").GetSingle();
            result.m10 = el.GetProperty("m10").GetSingle();
            result.m11 = el.GetProperty("m11").GetSingle();
            result.m12 = el.GetProperty("m12").GetSingle();
            result.m13 = el.GetProperty("m13").GetSingle();
            result.m20 = el.GetProperty("m20").GetSingle();
            result.m21 = el.GetProperty("m21").GetSingle();
            result.m22 = el.GetProperty("m22").GetSingle();
            result.m23 = el.GetProperty("m23").GetSingle();
            result.m30 = el.GetProperty("m30").GetSingle();
            result.m31 = el.GetProperty("m31").GetSingle();
            result.m32 = el.GetProperty("m32").GetSingle();
            result.m33 = el.GetProperty("m33").GetSingle();
            return result;
        }
    }
}
