using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public static class RszJsonSerializer
    {
        public static string Serialize(IRszNode node, JsonSerializerOptions? options = null)
        {
            var serializerOptions = CreateOptions(options);
            return JsonSerializer.Serialize(node, serializerOptions);
        }

        public static IRszNode Deserialize(string jsonDocument, JsonSerializerOptions? options = null)
        {
            return Deserialize(JsonDocument.Parse(jsonDocument), options);
        }

        public static IRszNode Deserialize(JsonDocument jsonDocument, JsonSerializerOptions? options = null)
        {
            var serializerOptions = CreateOptions(options);
            return JsonSerializer.Deserialize<IRszNode>(jsonDocument.RootElement.GetRawText(), serializerOptions)!;
        }

        public static IRszNode Deserialize(string jsonDocument, RszTypeRepository repository, JsonSerializerOptions? options = null)
        {
            return Deserialize(JsonDocument.Parse(jsonDocument), repository, options);
        }

        public static IRszNode Deserialize(JsonDocument jsonDocument, RszTypeRepository repository, JsonSerializerOptions? options = null)
        {
            var serializerOptions = CreateOptions(options, repository);
            return JsonSerializer.Deserialize<IRszNode>(jsonDocument.RootElement.GetRawText(), serializerOptions)!;
        }

        private static JsonSerializerOptions CreateOptions(JsonSerializerOptions? options, RszTypeRepository? repository = null)
        {
            var serializerOptions = options == null ? new JsonSerializerOptions() : new JsonSerializerOptions(options);
            serializerOptions.Converters.Add(new RszNodeJsonConverter(repository));
            return serializerOptions;
        }
    }

    public sealed class RszNodeJsonConverter : JsonConverter<IRszNode>
    {
        public static RszNodeJsonConverter Default { get; } = new RszNodeJsonConverter();
        private readonly RszTypeRepository? _repository;

        public RszNodeJsonConverter(RszTypeRepository? repository = null)
        {
            _repository = repository;
        }

        public override IRszNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return ReadNode(document.RootElement, options);
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
                writer.WritePropertyName("@guid");
                writer.WriteStringValue(gameObject.Guid);
                if (!string.IsNullOrEmpty(gameObject.Prefab))
                {
                    writer.WritePropertyName("@prefab");
                    writer.WriteStringValue(gameObject.Prefab);
                }
                WriteObjectNode(writer, gameObject.Settings, options);
                writer.WritePropertyName("@components");
                writer.WriteStartArray();
                foreach (var child in gameObject.Components)
                {
                    WriteNode(writer, child, options);
                }
                writer.WriteEndArray();
            }
            else if (value is RszObjectNode objectNode)
            {
                writer.WritePropertyName("@type");
                writer.WriteStringValue(objectNode.Type.Name);
                WriteObjectNode(writer, objectNode, options);
            }
            if (value is not RszObjectNode &&
                value is IRszNodeContainer container &&
                !container.Children.IsDefaultOrEmpty)
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
            var written = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var name = fields[i].Name;
                if (!written.Add(name))
                    name = $"v{i}";

                writer.WritePropertyName(name);
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
                writer.WriteStartArray();
                foreach (var child in arrayNode.Children)
                {
                    WriteNode(writer, child, options);
                }
                writer.WriteEndArray();
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

        private IRszNode ReadNode(JsonElement element, JsonSerializerOptions options)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ReadObject(element, options),
                JsonValueKind.Null => new RszNullNode(),
                _ => throw new NotSupportedException("Root JSON must be an object or null.")
            };
        }

        private IRszNode ReadObject(JsonElement element, JsonSerializerOptions options)
        {
            if (!element.TryGetProperty("@type", out var typeElement))
            {
                if (element.TryGetProperty("@children", out _))
                    return new RszScene(ReadSceneChildren(element, options));
                if (element.TryGetProperty("@path", out var resourcePath))
                    return new RszResourceNode(resourcePath.GetString());

                throw new NotSupportedException("Unable to infer RSZ node type from JSON.");
            }

            var typeName = typeElement.GetString() ?? throw new InvalidOperationException("Missing @type value.");
            if (typeName == "via.GameObject" && HasSceneMetadata(element))
                return ReadGameObject(element, options);
            if (typeName == "via.Folder" && element.TryGetProperty("@children", out _))
                return ReadFolder(element, options);
            if (element.TryGetProperty("@path", out var userDataPath) && IsUserDataNode(element))
                return new RszUserDataNode(ResolveType(typeName), userDataPath.GetString() ?? "");

            return ReadObjectNode(element, ResolveType(typeName), options);
        }

        private RszFolder ReadFolder(JsonElement element, JsonSerializerOptions options)
        {
            var settings = ReadObjectNode(element, ResolveType("via.Folder"), options);
            return new RszFolder(settings, ReadSceneChildren(element, options));
        }

        private RszGameObject ReadGameObject(JsonElement element, JsonSerializerOptions options)
        {
            var settings = ReadObjectNode(element, ResolveType("via.GameObject"), options);
            var components = ImmutableArray<RszObjectNode>.Empty;
            if (element.TryGetProperty("@components", out var componentsElement))
            {
                components = [.. componentsElement.EnumerateArray().Select(x => ReadObjectNode(x, ResolveType(x.GetProperty("@type").GetString()!), options))];
            }

            var guid = element.TryGetProperty("@guid", out var guidElement)
                ? guidElement.GetGuid()
                : Guid.Empty;
            var prefab = element.TryGetProperty("@prefab", out var prefabElement)
                ? prefabElement.GetString()
                : null;

            return new RszGameObject(guid, prefab, settings, components, ReadSceneChildren(element, options).Cast<RszGameObject>().ToImmutableArray());
        }

        private ImmutableArray<IRszSceneNode> ReadSceneChildren(JsonElement element, JsonSerializerOptions options)
        {
            if (!element.TryGetProperty("@children", out var childrenElement))
                return [];

            return [.. childrenElement.EnumerateArray().Select(x =>
            {
                var child = ReadObject(x, options);
                return child as IRszSceneNode ?? throw new NotSupportedException("Scene children must be folders or game objects.");
            })];
        }

        private RszObjectNode ReadObjectNode(JsonElement element, RszType type, JsonSerializerOptions options)
        {
            var defaults = type.Create();
            var children = ImmutableArray.CreateBuilder<IRszNode>(type.Fields.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < type.Fields.Length; i++)
            {
                var field = type.Fields[i];
                var propertyName = seen.Add(field.Name) ? field.Name : $"v{i}";
                if (!element.TryGetProperty(propertyName, out var propertyElement))
                {
                    children.Add(defaults.Children[i]);
                    continue;
                }

                children.Add(field.IsArray
                    ? ReadArrayNode(propertyElement, field, options)
                    : ReadFieldNode(propertyElement, field, options));
            }
            return new RszObjectNode(type, children.ToImmutable());
        }

        private IRszNode ReadArrayNode(JsonElement element, RszTypeField field, JsonSerializerOptions options)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new NotSupportedException($"Expected array for field '{field.Name}'.");

            var children = ImmutableArray.CreateBuilder<IRszNode>();
            foreach (var child in element.EnumerateArray())
            {
                children.Add(ReadNodeForField(child, field.Type, field.ObjectType, options));
            }
            return new RszArrayNode(field.Type, children.ToImmutable());
        }

        private IRszNode ReadFieldNode(JsonElement element, RszTypeField field, JsonSerializerOptions options)
        {
            return ReadNodeForField(element, field.Type, field.ObjectType, options);
        }

        private IRszNode ReadNodeForField(JsonElement element, RszFieldType fieldType, RszType? objectType, JsonSerializerOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return fieldType switch
                {
                    RszFieldType.Resource => new RszResourceNode(),
                    RszFieldType.UserData => new RszUserDataNode(),
                    RszFieldType.Object => new RszNullNode(),
                    _ => new RszNullNode()
                };
            }

            return fieldType switch
            {
                RszFieldType.Object => ReadObjectNode(element, ResolveObjectType(element, objectType), options),
                RszFieldType.String or RszFieldType.RuntimeType => new RszStringNode(element.GetString() ?? ""),
                RszFieldType.Resource => new RszResourceNode(element.GetProperty("@path").GetString()),
                RszFieldType.UserData => new RszUserDataNode(
                    ResolveType(element.GetProperty("@type").GetString() ?? throw new InvalidOperationException("Missing @type.")),
                    element.GetProperty("@path").GetString() ?? ""),
                _ => RszSerializer.Serialize(fieldType, ReadValue(element, fieldType, options), _repository)
            };
        }

        private object ReadValue(JsonElement element, RszFieldType fieldType, JsonSerializerOptions options)
        {
            var clrType = GetValueClrType(fieldType);
            return JsonSerializer.Deserialize(element.GetRawText(), clrType, options)
                ?? throw new InvalidOperationException($"Unable to deserialize {fieldType}.");
        }

        private RszType ResolveObjectType(JsonElement element, RszType? expectedType)
        {
            if (element.TryGetProperty("@type", out var typeElement))
                return ResolveType(typeElement.GetString() ?? throw new InvalidOperationException("Missing @type."), expectedType);
            if (expectedType != null)
                return expectedType;
            throw new InvalidOperationException("Unable to resolve object type without @type metadata.");
        }

        private RszType ResolveType(string typeName, RszType? expectedType = null)
        {
            var repository = _repository ?? throw new InvalidOperationException("A type repository is required to deserialize RSZ JSON.");
            var type = repository.FromName(typeName) ?? throw new InvalidOperationException($"Type '{typeName}' was not found.");
            if (expectedType == null)
                return type;

            if (type == expectedType)
                return type;

            var current = type.Parent;
            while (current != null)
            {
                if (current == expectedType)
                    return type;
                current = current.Parent;
            }

            throw new InvalidOperationException($"Type '{typeName}' is not assignable to '{expectedType.Name}'.");
        }

        private static bool HasSceneMetadata(JsonElement element)
        {
            return element.TryGetProperty("@guid", out _)
                || element.TryGetProperty("@prefab", out _)
                || element.TryGetProperty("@components", out _)
                || element.TryGetProperty("@children", out _);
        }

        private static bool IsUserDataNode(JsonElement element)
        {
            return element.EnumerateObject().All(x => x.Name is "@type" or "@path");
        }

        private static Type GetValueClrType(RszFieldType type)
        {
            return type switch
            {
                RszFieldType.Bool => typeof(bool),
                RszFieldType.S8 => typeof(sbyte),
                RszFieldType.U8 => typeof(byte),
                RszFieldType.S16 => typeof(short),
                RszFieldType.U16 => typeof(ushort),
                RszFieldType.S32 => typeof(int),
                RszFieldType.U32 => typeof(uint),
                RszFieldType.S64 => typeof(long),
                RszFieldType.U64 => typeof(ulong),
                RszFieldType.F32 => typeof(float),
                RszFieldType.F64 => typeof(double),
                RszFieldType.Vec2 => typeof(Vector2),
                RszFieldType.Vec3 => typeof(Vector3),
                RszFieldType.Vec4 => typeof(Vector4),
                RszFieldType.Mat4 => typeof(Matrix4x4),
                RszFieldType.Quaternion => typeof(Quaternion),
                RszFieldType.Guid or RszFieldType.GameObjectRef => typeof(Guid),
                RszFieldType.Uint2 => typeof(global::via.Uint2),
                RszFieldType.Uint3 => typeof(global::via.Uint3),
                RszFieldType.Uint4 => typeof(global::via.Uint4),
                RszFieldType.Int2 => typeof(global::via.Int2),
                RszFieldType.Int3 => typeof(global::via.Int3),
                RszFieldType.Int4 => typeof(global::via.Int4),
                RszFieldType.Color => typeof(global::via.Color),
                RszFieldType.AABB => typeof(global::via.AABB),
                RszFieldType.Capsule => typeof(global::via.Capsule),
                RszFieldType.TaperedCapsule => typeof(global::via.TaperedCapsule),
                RszFieldType.Cone => typeof(global::via.Cone),
                RszFieldType.Line => typeof(global::via.Line),
                RszFieldType.LineSegment => typeof(global::via.LineSegment),
                RszFieldType.OBB => typeof(global::via.OBB),
                RszFieldType.Plane => typeof(global::via.Plane),
                RszFieldType.PlaneXZ => typeof(global::via.PlaneXZ),
                RszFieldType.Point => typeof(global::via.Point),
                RszFieldType.Range => typeof(global::via.Range),
                RszFieldType.RangeI => typeof(global::via.RangeI),
                RszFieldType.Ray => typeof(global::via.Ray),
                RszFieldType.RayY => typeof(global::via.RayY),
                RszFieldType.Segment => typeof(global::via.Segment),
                RszFieldType.Size => typeof(global::via.Size),
                RszFieldType.Sphere => typeof(global::via.Sphere),
                RszFieldType.Triangle => typeof(global::via.Triangle),
                RszFieldType.Cylinder => typeof(global::via.Cylinder),
                RszFieldType.Ellipsoid => typeof(global::via.Ellipsoid),
                RszFieldType.Area => typeof(global::via.Area),
                RszFieldType.Torus => typeof(global::via.Torus),
                RszFieldType.Rect => typeof(global::via.Rect),
                RszFieldType.Rect3D => typeof(global::via.Rect3D),
                RszFieldType.Frustum => typeof(global::via.Frustum),
                RszFieldType.KeyFrame => typeof(global::via.KeyFrame),
                RszFieldType.Sfix => typeof(global::via.sfix),
                RszFieldType.Sfix2 => typeof(global::via.Sfix2),
                RszFieldType.Sfix3 => typeof(global::via.Sfix3),
                RszFieldType.Sfix4 => typeof(global::via.Sfix4),
                RszFieldType.Position => typeof(global::via.Position),
                _ => throw new NotSupportedException($"Unsupported RSZ value type '{type}'.")
            };
        }
    }
}
