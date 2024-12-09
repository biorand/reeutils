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
        public string Serialize(RszInstance instance, JsonSerializerOptions? options = null)
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
                    value = ToDictionary(child);
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

        public RszInstance Deserialize(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new Exception("Root must be an object");

            return DeserializeObject(el);
        }

        private RszInstance DeserializeObject(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new Exception("Expected object");

            var type = el.GetStringProperty("$type")!;
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
                    RszFieldType.Data => throw new NotImplementedException(),
                    _ => throw new NotImplementedException($"Deserialization of {field.type} is not implemented")
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Issue with setting field {field.name} to {el}. {ex.Message}", ex);
            }
        }
    }
}
