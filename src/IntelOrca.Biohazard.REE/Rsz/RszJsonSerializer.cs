using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public static class RszJsonSerializer
    {
        public static string Serialize(IRszNode node, JsonSerializerOptions? options = null)
        {
            options ??= new JsonSerializerOptions();
            options.Converters.Add(RszNodeJsonConverter.Default);
            return JsonSerializer.Serialize(node, options);
        }

        public static IRszNode Deserialize(string jsonDocument, JsonSerializerOptions? options = null)
        {
            return Deserialize(JsonDocument.Parse(jsonDocument), options);
        }

        public static IRszNode Deserialize(JsonDocument jsonDocument, JsonSerializerOptions? options = null)
        {
            options ??= new JsonSerializerOptions();
            options.Converters.Add(RszNodeJsonConverter.Default);
            return JsonSerializer.Deserialize<IRszNode>(jsonDocument, options)!;
        }
    }

    public sealed class RszNodeJsonConverter : JsonConverter<IRszNode>
    {
        public static RszNodeJsonConverter Default { get; } = new RszNodeJsonConverter();

        private RszNodeJsonConverter()
        {
        }

        public override IRszNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, IRszNode value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value is RszScene scene)
            {
            }
            else if (value is RszFolder folder)
            {
                writer.WritePropertyName("@type");
                writer.WriteStringValue("via.Folder");
                WriteObjectNode(writer, folder.Settings, options);
            }
            else if (value is RszGameObject gameObject)
            {
                writer.WritePropertyName("@type");
                writer.WriteStringValue("via.GameObject");
                WriteObjectNode(writer, gameObject.Settings, options);
                writer.WritePropertyName("@components");
                writer.WriteStartArray();
                foreach (var child in gameObject.Components)
                {
                    WriteNode(writer, child, options);
                }
                writer.WriteEndArray();
            }
            if (value is IRszNodeContainer container && !container.Children.IsDefaultOrEmpty)
            {
                writer.WritePropertyName("@children");
                writer.WriteStartArray();
                foreach (var child in container.Children)
                {
                    Write(writer, child, options);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        private static void WriteObjectNode(Utf8JsonWriter writer, RszObjectNode node, JsonSerializerOptions options)
        {
            var fields = node.Type.Fields;
            var children = node.Children;

            var count = fields.Length;
            for (var i = 0; i < count; i++)
            {
                var field = fields[i];
                writer.WritePropertyName(field.Name);
                WriteNode(writer, children[i], options);
            }
        }

        private static void WriteNode(Utf8JsonWriter writer, IRszNode node, JsonSerializerOptions options)
        {
            if (node is RszObjectNode objectNode)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@type");
                writer.WriteStringValue(objectNode.Type.Name);
                WriteObjectNode(writer, objectNode, options);
                writer.WriteEndObject();
            }
            else if (node is RszArrayNode arrayNode)
            {
                writer.WriteStartObject();
                foreach (var child in arrayNode.Children)
                {
                    WriteNode(writer, child, options);
                }
                writer.WriteEndObject();
            }
            else if (node is RszStringNode stringNode)
            {
                writer.WriteStringValue(stringNode.Value);
            }
            else if (node is RszResourceNode resourceNode)
            {
                if (resourceNode.IsEmpty)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("@path");
                    writer.WriteStringValue(resourceNode.Value);
                    writer.WriteEndObject();
                }
            }
            else if (node is RszUserDataNode userDataNode)
            {
                if (userDataNode.IsEmpty)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("@type");
                    writer.WriteStringValue(userDataNode.Type.Name);
                    writer.WritePropertyName("@path");
                    writer.WriteStringValue(userDataNode.Path);
                    writer.WriteEndObject();
                }
            }
            else if (node is RszNullNode)
            {
                writer.WriteNullValue();
            }
            else if (node is RszValueNode valueNode)
            {
                var value = RszSerializer.Deserialize(valueNode);
                var valueToSerialize = value switch
                {
                    Vector2 vec2 => new { vec2.X, vec2.Y },
                    Vector3 vec3 => new { vec3.X, vec3.Y, vec3.Z },
                    Vector4 vec4 => new { vec4.X, vec4.Y, vec4.Z, vec4.W },
                    Quaternion quaternion => new { quaternion.X, quaternion.Y, quaternion.Z, quaternion.W },
                    _ => value
                };
                JsonSerializer.Serialize(writer, valueToSerialize, options);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
