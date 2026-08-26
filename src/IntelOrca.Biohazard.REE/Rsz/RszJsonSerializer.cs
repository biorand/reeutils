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

        public static string Serialize(IRszNode node, RszTypeRepository repository, JsonSerializerOptions? options = null)
        {
            var serializerOptions = CreateOptions(options, repository);
            return JsonSerializer.Serialize(node, serializerOptions);
        }

        public static IRszNode Deserialize(JsonDocument jsonDocument, RszTypeRepository repository, JsonSerializerOptions? options = null)
        {
            var serializerOptions = CreateOptions(options, repository);
            return JsonSerializer.Deserialize<IRszNode>(jsonDocument.RootElement.GetRawText(), serializerOptions)!;
        }

        /// <summary>
        /// Serializes a scene together with standalone ("loose") objects that are not owned by
        /// any game object. RE2 (v16) prefabs append such objects after the game object table;
        /// they share node identity with scene nodes, so both must be written through a single
        /// converter pass for @id/@ref annotations to cover the whole document.
        /// </summary>
        public static string SerializePrefabDocument(RszScene scene, IReadOnlyList<RszObjectNode> looseObjects, RszTypeRepository repository, JsonSerializerOptions? options = null)
        {
            var serializerOptions = CreateOptions(options, repository);
            var document = new PrefabDocument(scene, looseObjects.ToImmutableArray());
            return JsonSerializer.Serialize<IRszNode>(document, serializerOptions);
        }

        /// <summary>
        /// Deserializes a document produced by <see cref="SerializePrefabDocument"/>. Bare
        /// scene documents (no "@scene" property) are also accepted; loose objects are then
        /// empty.
        /// </summary>
        public static RszScene DeserializePrefabScene(string jsonDocument, RszTypeRepository repository, out ImmutableArray<RszObjectNode> looseObjects)
        {
            return DeserializePrefabScene(JsonDocument.Parse(jsonDocument), repository, out looseObjects);
        }

        public static RszScene DeserializePrefabScene(JsonDocument jsonDocument, RszTypeRepository repository, out ImmutableArray<RszObjectNode> looseObjects)
        {
            var node = Deserialize(jsonDocument, repository);
            if (node is PrefabDocument prefabDocument)
            {
                looseObjects = prefabDocument.LooseObjects;
                return prefabDocument.Scene;
            }
            if (node is RszScene scene)
            {
                looseObjects = [];
                return scene;
            }
            throw new InvalidOperationException("JSON document does not contain a scene.");
        }

        private static JsonSerializerOptions CreateOptions(JsonSerializerOptions? options, RszTypeRepository? repository = null)
        {
            var serializerOptions = options == null ? new JsonSerializerOptions() : new JsonSerializerOptions(options);
            serializerOptions.Converters.Add(new RszNodeJsonConverter(repository));
            return serializerOptions;
        }
    }

    /// <summary>
    /// Container for a scene plus standalone loose objects, serialized as
    /// { "@loose": [...], "@scene": {...} }. Only used as the root of prefab documents.
    /// </summary>
    internal sealed class PrefabDocument(RszScene scene, ImmutableArray<RszObjectNode> looseObjects) : IRszNodeContainer
    {
        public RszScene Scene { get; } = scene;
        public ImmutableArray<RszObjectNode> LooseObjects { get; } = looseObjects;

        ImmutableArray<IRszNode> IRszNodeContainer.Children =>
            LooseObjects.CastArray<IRszNode>().Add(Scene);

        IRszNodeContainer IRszNodeContainer.WithChildren(ImmutableArray<IRszNode> children) => this;
    }

    public sealed class RszNodeJsonConverter : JsonConverter<IRszNode>
    {
        public static RszNodeJsonConverter Default { get; } = new RszNodeJsonConverter();
        private readonly RszTypeRepository? _repository;

        // Per-document state. RSZ v8 (RE2 non-RT) files rely on node reference-identity:
        // one logical object (or embedded userdata blob) is shared by several owners and the
        // builder de-duplicates instances by node identity. The JSON hop must preserve that
        // sharing, so nodes that occur more than once are annotated with "@id" on first
        // occurrence and later occurrences are written as { "@ref": id }.
        private HashSet<object>? _sharedNodes;
        private Dictionary<object, int>? _idsByNode;
        private Dictionary<int, IRszNode>? _nodesById;
        private Dictionary<RszFile, ImmutableArray<RszObjectNode>>? _embeddedObjects;
        private int _nextId;
        private bool _initialized;

        public RszNodeJsonConverter(RszTypeRepository? repository = null)
        {
            _repository = repository;
        }

        public override IRszNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            _nodesById ??= [];
            using var document = JsonDocument.ParseValue(ref reader);
            return ReadNode(document.RootElement, options);
        }

        public override void Write(Utf8JsonWriter writer, IRszNode value, JsonSerializerOptions options)
        {
            if (!_initialized)
            {
                _initialized = true;
                _idsByNode = [];
                _nodesById = [];
                _nextId = 0;
                _sharedNodes = CountSharedNodes(value);
            }

            if (TryWriteBackReference(writer, value))
                return;

            writer.WriteStartObject();
            WriteShareId(writer, value);
            if (value is PrefabDocument prefabDocument)
            {
                // Loose objects are written first: scene nodes that reference them then
                // serialize as {"@ref": id}, keeping one instance per logical object.
                writer.WritePropertyName("@loose");
                writer.WriteStartArray();
                foreach (var loose in prefabDocument.LooseObjects)
                {
                    WriteNode(writer, loose, options);
                }
                writer.WriteEndArray();
                writer.WritePropertyName("@scene");
                Write(writer, prefabDocument.Scene, options);
                writer.WriteEndObject();
                return;
            }
            if (value is RszScene)
            {
                // Scenes have no settings of their own; "@children" (written below) is the
                // only marker that lets a document round-trip as a scene.
            }
            else if (value is RszFolder folder)
            {
                writer.WritePropertyName("@type");
                writer.WriteStringValue("via.Folder");
                WriteObjectNode(writer, folder.Settings, options);
                if (folder.Children.IsDefaultOrEmpty)
                {
                    // Empty folders still need the marker so they re-import as folders.
                    writer.WritePropertyName("@children");
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                }
            }
            else if (value is RszGameObject gameObject)
            {
                writer.WritePropertyName("@type");
                writer.WriteStringValue("via.GameObject");
                writer.WritePropertyName("@guid");
                writer.WriteStringValue(gameObject.Guid);
                writer.WritePropertyName("@padding");
                writer.WriteNumberValue(gameObject.Padding);
                if (!string.IsNullOrEmpty(gameObject.Prefab))
                {
                    writer.WritePropertyName("@prefab");
                    writer.WriteStringValue(gameObject.Prefab);
                }
                WriteObjectNode(writer, gameObject.Settings, options);
                if (!gameObject.Components.IsDefaultOrEmpty)
                {
                    writer.WritePropertyName("@components");
                    writer.WriteStartArray();
                    foreach (var child in gameObject.Components)
                    {
                        WriteNode(writer, child, options);
                    }
                    writer.WriteEndArray();
                }
            }
            else if (value is RszObjectNode objectNode)
            {
                writer.WritePropertyName("@type");
                writer.WriteStringValue(objectNode.Type.Name);
                WriteObjectNode(writer, objectNode, options);
            }
            if (value is not RszObjectNode &&
                value is IRszNodeContainer container &&
                (!container.Children.IsDefaultOrEmpty || value is RszScene))
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

        private void WriteObjectNode(Utf8JsonWriter writer, RszObjectNode node, JsonSerializerOptions options)
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

        private bool IsShareable(IRszNode node) => node is RszObjectNode or RszEmbeddedUserValueNode;

        private bool TryWriteBackReference(Utf8JsonWriter writer, IRszNode node)
        {
            if (_sharedNodes != null && _idsByNode != null &&
                IsShareable(node) &&
                _sharedNodes.Contains(node) &&
                _idsByNode.TryGetValue(node, out var id))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@ref");
                writer.WriteNumberValue(id);
                writer.WriteEndObject();
                return true;
            }
            return false;
        }

        private void WriteShareId(Utf8JsonWriter writer, IRszNode node)
        {
            if (_sharedNodes != null && _idsByNode != null &&
                IsShareable(node) &&
                _sharedNodes.Contains(node) &&
                !_idsByNode.ContainsKey(node))
            {
                var id = _nextId++;
                _idsByNode.Add(node, id);
                writer.WritePropertyName("@id");
                writer.WriteNumberValue(id);
            }
        }

        // Counts shareable nodes that are reachable more than once so only genuinely shared
        // nodes get "@id"/"@ref" annotations. Embedded userdata graphs participate too: two
        // fields can reference the same blob, and blobs can share objects internally.
        private HashSet<object> CountSharedNodes(IRszNode root)
        {
            _embeddedObjects = [];
            var counts = new Dictionary<object, int>();
            Visit(root);

            var shared = new HashSet<object>();
            foreach (var (node, count) in counts)
            {
                if (count > 1)
                    shared.Add(node);
            }
            return shared;

            void Visit(IRszNode node)
            {
                if (IsShareable(node))
                {
                    counts.TryGetValue(node, out var count);
                    counts[node] = count + 1;
                    if (count > 0)
                        return; // subtree already counted on the first occurrence
                }

                switch (node)
                {
                    case PrefabDocument prefabDocument:
                        foreach (var loose in prefabDocument.LooseObjects) Visit(loose);
                        Visit(prefabDocument.Scene);
                        break;
                    case RszScene scene:
                        foreach (var child in scene.Children) Visit(child);
                        break;
                    case RszFolder folder:
                        Visit(folder.Settings);
                        foreach (var child in folder.Children) Visit(child);
                        break;
                    case RszGameObject gameObject:
                        Visit(gameObject.Settings);
                        foreach (var component in gameObject.Components) Visit(component);
                        foreach (var child in gameObject.Children) Visit(child);
                        break;
                    case RszObjectNode objectNode:
                        foreach (var child in objectNode.Children) Visit(child);
                        break;
                    case RszArrayNode arrayNode:
                        foreach (var child in arrayNode.Children) Visit(child);
                        break;
                    case RszEmbeddedUserValueNode embedded when _repository != null:
                        if (!_embeddedObjects!.TryGetValue(embedded.Embedded, out var objects))
                        {
                            objects = embedded.Embedded.ReadObjectList(_repository);
                            _embeddedObjects.Add(embedded.Embedded, objects);
                        }
                        foreach (var obj in objects) Visit(obj);
                        break;
                }
            }
        }

        private void WriteEmbeddedUserValue(Utf8JsonWriter writer, RszEmbeddedUserValueNode embeddedNode, JsonSerializerOptions options)
        {
            if (TryWriteBackReference(writer, embeddedNode))
                return;

            // RE2-era RSZ streams store userdata inline rather than by path; expose it as
            // fully parsed nested JSON so it round-trips through export/import.
            var repository = _repository
                ?? throw new InvalidOperationException("A type repository is required to serialize embedded userdata.");
            var objects = _embeddedObjects != null && _embeddedObjects.TryGetValue(embeddedNode.Embedded, out var cached)
                ? cached
                : embeddedNode.Embedded.ReadObjectList(repository);
            writer.WriteStartObject();
            WriteShareId(writer, embeddedNode);
            writer.WritePropertyName("@embedded");
            writer.WriteStartObject();
            writer.WritePropertyName("@type");
            writer.WriteStringValue(embeddedNode.Type.Name);
            writer.WritePropertyName("@hash");
            writer.WriteNumberValue(embeddedNode.Hash);
            writer.WritePropertyName("@objects");
            writer.WriteStartArray();
            foreach (var obj in objects)
            {
                WriteNode(writer, obj, options);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private void WriteNode(Utf8JsonWriter writer, IRszNode node, JsonSerializerOptions options)
        {
            if (node is RszObjectNode objectNode)
            {
                if (TryWriteBackReference(writer, node))
                    return;
                writer.WriteStartObject();
                WriteShareId(writer, node);
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
                // Null and empty-string resources serialise differently on disk
                // (0 vs len+1+NUL), so the distinction must survive the JSON hop.
                if (resourceNode.Value == null)
                {
                    writer.WriteNullValue();
                }
                else if (resourceNode.Value.Length == 0)
                {
                    writer.WriteStringValue("");
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
            else if (node is RszEmbeddedUserValueNode embeddedNode)
            {
                WriteEmbeddedUserValue(writer, embeddedNode, options);
            }
            else if (node is RszNullNode)
            {
                writer.WriteNullValue();
            }
            else if (node is RszValueNode valueNode)
            {
                var value = RszSerializer.Deserialize(valueNode);
                if (value is Matrix4x4 matrix)
                {
                    WriteMatrix4x4(writer, matrix);
                    return;
                }
                if (s_rawValueTypes.Contains(valueNode.Type))
                {
                    // via.* native structs are written as raw little-endian bytes so every
                    // component survives the roundtrip regardless of STJ struct support.
                    writer.WriteStringValue(Convert.ToBase64String(valueNode.Data.Span));
                    return;
                }
                var valueToSerialize = value switch
                {
                    Vector2 vec2 => new { vec2.X, vec2.Y },
                    Vector3 vec3 => new { vec3.X, vec3.Y, vec3.Z },
                    Vector4 vec4 => new { vec4.X, vec4.Y, vec4.Z, vec4.W },
                    Quaternion quaternion => new { quaternion.X, quaternion.Y, quaternion.Z, quaternion.W },
                    // Uri-typed fields can hold GameObjectRef guid values (RE2 v16).
                    Guid guid when valueNode.Type == RszFieldType.Uri => new { @ref = guid },
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
            return ReadMaybeReferenced(element, () => ReadObjectCore(element, options));
        }

        private IRszNode ReadMaybeReferenced(JsonElement element, Func<IRszNode> factory)
        {
            if (element.TryGetProperty("@ref", out var refElement))
            {
                var id = refElement.GetInt32();
                if (_nodesById != null && _nodesById.TryGetValue(id, out var shared))
                    return shared;
                throw new InvalidOperationException($"Reference @ref {id} could not be resolved; the referenced node must appear earlier in the document.");
            }

            var node = factory();
            if (_nodesById != null && element.TryGetProperty("@id", out var idElement))
                _nodesById[idElement.GetInt32()] = node;
            return node;
        }

        private IRszNode ReadObjectCore(JsonElement element, JsonSerializerOptions options)
        {
            if (!element.TryGetProperty("@type", out var typeElement))
            {
                if (element.TryGetProperty("@scene", out var sceneElement))
                {
                    // Prefab document: { "@loose": [...], "@scene": {...} }
                    var loose = ImmutableArray.CreateBuilder<RszObjectNode>();
                    if (element.TryGetProperty("@loose", out var looseElement) &&
                        looseElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var looseItem in looseElement.EnumerateArray())
                        {
                            loose.Add((RszObjectNode)ReadMaybeReferenced(looseItem, () =>
                                ReadObjectNode(looseItem, ResolveType(looseItem.GetProperty("@type").GetString()!), options)));
                        }
                    }
                    var scene = (RszScene)ReadNode(sceneElement, options);
                    return new PrefabDocument(scene, loose.Count == 0 ? [] : loose.ToImmutable());
                }
                if (element.TryGetProperty("@children", out _))
                    return new RszScene(ReadSceneChildren(element, options));
                if (element.TryGetProperty("@path", out var resourcePath))
                    return new RszResourceNode(resourcePath.GetString());

                throw new NotSupportedException("Unable to infer RSZ node type from JSON.");
            }

            var typeName = typeElement.GetString() ?? throw new InvalidOperationException("Missing @type value.");
            if (typeName == "via.GameObject" && HasSceneMetadata(element))
                return ReadGameObject(element, options);
            if (typeName == "via.Folder")
                return ReadFolder(element, options);
            if (element.TryGetProperty("@path", out var userDataPath) && IsUserDataNode(element))
                return new RszUserDataNode(ResolveType(typeName), userDataPath.GetString() ?? "");
            if (typeName == "via.GameObject" && element.TryGetProperty("@embedded", out _))
                return ReadEmbeddedUserValue(element.GetProperty("@embedded"), typeName, options);

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
            var padding = element.TryGetProperty("@padding", out var paddingElement)
                ? paddingElement.GetInt16()
                : RszGameObject.UnknownPadding;
            var prefab = element.TryGetProperty("@prefab", out var prefabElement)
                ? prefabElement.GetString()
                : null;

            return new RszGameObject(guid, prefab, padding, settings, components, ReadSceneChildren(element, options).Cast<RszGameObject>().ToImmutableArray());
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
                    RszFieldType.Resource => new RszResourceNode(null),
                    RszFieldType.UserData => new RszUserDataNode(),
                    RszFieldType.Object => new RszNullNode(),
                    _ => new RszNullNode()
                };
            }

            // Node back-references ({ "@ref": <number> }) are numeric; guid-style references
            // in value fields are strings, so the two never collide.
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("@ref", out var nodeRefElement) &&
                nodeRefElement.ValueKind == JsonValueKind.Number)
            {
                return ReadMaybeReferenced(element, () => throw new InvalidOperationException("Unreachable."));
            }

            return fieldType switch
            {
                RszFieldType.Object => ReadMaybeReferenced(element, () => ReadObjectNode(element, ResolveObjectType(element, objectType), options)),
                RszFieldType.Struct => ReadObjectNode(element, objectType ?? ResolveObjectType(element, null), options),
                RszFieldType.String or RszFieldType.RuntimeType => new RszStringNode(element.GetString() ?? ""),
                RszFieldType.Resource => element.ValueKind == JsonValueKind.String
                    ? new RszResourceNode(element.GetString() ?? "")
                    : new RszResourceNode(element.GetProperty("@path").GetString()),
                RszFieldType.UserData when element.TryGetProperty("@embedded", out var embeddedElement)
                    => ReadMaybeReferenced(element, () => ReadEmbeddedUserValue(embeddedElement, ResolveObjectType(element, objectType).Name, options)),
                RszFieldType.UserData => new RszUserDataNode(
                    ResolveType(element.GetProperty("@type").GetString() ?? throw new InvalidOperationException("Missing @type.")),
                    element.GetProperty("@path").GetString() ?? ""),
                RszFieldType.Uri when element.TryGetProperty("@ref", out var uriRefElement)
                    => RszSerializer.Serialize(fieldType, ReadGuidValue(uriRefElement), _repository),
                // The camelCase naming policy lowercases "@ref" to "ref" in exported JSON.
                RszFieldType.Guid or RszFieldType.GameObjectRef or RszFieldType.Uri
                    when element.ValueKind == JsonValueKind.Object &&
                         (element.TryGetProperty("ref", out var refElement) || element.TryGetProperty("@ref", out refElement))
                    => RszSerializer.Serialize(fieldType, ReadGuidValue(refElement), _repository),
                _ => RszSerializer.Serialize(fieldType, ReadValue(element, fieldType, options), _repository)
            };
        }

        private static Guid ReadGuidValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() is { } s ? Guid.Parse(s) : Guid.Empty,
                _ => element.GetGuid()
            };
        }

        private object ReadValue(JsonElement element, RszFieldType fieldType, JsonSerializerOptions options)
        {
            // System.Text.Json cannot reliably populate numeric struct properties (Vector3 etc.)
            // on every target framework; parse the well-known shapes manually so values never
            // silently default to zero.
            switch (fieldType)
            {
                case RszFieldType.Vec2:
                case RszFieldType.Float2:
                    return new Vector2(Num(element, "X"), Num(element, "Y"));
                case RszFieldType.Vec3:
                case RszFieldType.Float3:
                    return new Vector3(Num(element, "X"), Num(element, "Y"), Num(element, "Z"));
                case RszFieldType.Vec4:
                case RszFieldType.Float4:
                    return new Vector4(Num(element, "X"), Num(element, "Y"), Num(element, "Z"), Num(element, "W"));
                case RszFieldType.Quaternion:
                    return new Quaternion(Num(element, "X"), Num(element, "Y"), Num(element, "Z"), Num(element, "W"));
                case RszFieldType.Mat4:
                    return ReadMatrix4x4(element);
            }

            if (s_rawValueTypes.Contains(fieldType))
            {
                // via.* native structs cannot be reliably populated by System.Text.Json
                // (get-only properties / field-only shapes); they are exported as the raw
                // little-endian bytes, base64-encoded. Accept that form here; fall back to
                // STJ for legacy documents that carried the object shape.
                if (element.ValueKind == JsonValueKind.String)
                {
                    var bytes = Convert.FromBase64String(element.GetString() ?? "");
                    return RszSerializer.Serialize(fieldType, bytes, _repository);
                }
            }

            var clrType = GetValueClrType(fieldType);
            // Value objects are written with their CLR property names; deserialize case-insensitively
            // so naming policies on the surrounding document don't affect round-tripping.
            return JsonSerializer.Deserialize(element.GetRawText(), clrType, s_valueReadOptions)
                ?? throw new InvalidOperationException($"Unable to deserialize {fieldType}.");
        }

        private static float Num(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var v))
                return 0f;
            return v.ValueKind == JsonValueKind.Number ? v.GetSingle() : 0f;
        }

        private static void WriteMatrix4x4(Utf8JsonWriter writer, Matrix4x4 m)
        {
            writer.WriteStartObject();
            writer.WriteNumber("M11", m.M11);
            writer.WriteNumber("M12", m.M12);
            writer.WriteNumber("M13", m.M13);
            writer.WriteNumber("M14", m.M14);
            writer.WriteNumber("M21", m.M21);
            writer.WriteNumber("M22", m.M22);
            writer.WriteNumber("M23", m.M23);
            writer.WriteNumber("M24", m.M24);
            writer.WriteNumber("M31", m.M31);
            writer.WriteNumber("M32", m.M32);
            writer.WriteNumber("M33", m.M33);
            writer.WriteNumber("M34", m.M34);
            writer.WriteNumber("M41", m.M41);
            writer.WriteNumber("M42", m.M42);
            writer.WriteNumber("M43", m.M43);
            writer.WriteNumber("M44", m.M44);
            writer.WriteEndObject();
        }

        private static Matrix4x4 ReadMatrix4x4(JsonElement element)
        {
            return new Matrix4x4(
                Num(element, "M11"), Num(element, "M12"), Num(element, "M13"), Num(element, "M14"),
                Num(element, "M21"), Num(element, "M22"), Num(element, "M23"), Num(element, "M24"),
                Num(element, "M31"), Num(element, "M32"), Num(element, "M33"), Num(element, "M34"),
                Num(element, "M41"), Num(element, "M42"), Num(element, "M43"), Num(element, "M44"));
        }

        private static readonly JsonSerializerOptions s_valueReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        // Native via.* struct value types exported as base64 raw bytes (see ReadValue/WriteNode).
        private static readonly HashSet<RszFieldType> s_rawValueTypes = new()
        {
            RszFieldType.Uint2, RszFieldType.Uint3, RszFieldType.Uint4,
            RszFieldType.Int2, RszFieldType.Int3, RszFieldType.Int4,
            RszFieldType.Color, RszFieldType.AABB, RszFieldType.Capsule,
            RszFieldType.TaperedCapsule, RszFieldType.Cone, RszFieldType.Line,
            RszFieldType.LineSegment, RszFieldType.OBB, RszFieldType.Plane,
            RszFieldType.PlaneXZ, RszFieldType.Point, RszFieldType.Range,
            RszFieldType.RangeI, RszFieldType.Ray, RszFieldType.RayY,
            RszFieldType.Segment, RszFieldType.Size, RszFieldType.Sphere,
            RszFieldType.Triangle, RszFieldType.Cylinder, RszFieldType.Ellipsoid,
            RszFieldType.Area, RszFieldType.Torus, RszFieldType.Rect, RszFieldType.Rect3D,
            RszFieldType.Frustum, RszFieldType.KeyFrame, RszFieldType.Sfix,
            RszFieldType.Sfix2, RszFieldType.Sfix3, RszFieldType.Sfix4,
            RszFieldType.Position, RszFieldType.Mat3, RszFieldType.Float3x3,
            RszFieldType.Float3x4, RszFieldType.Float4x3, RszFieldType.Float4x4,
            RszFieldType.Half2, RszFieldType.Half4, RszFieldType.VecU4
        };

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

        private RszEmbeddedUserValueNode ReadEmbeddedUserValue(JsonElement element, string typeName, JsonSerializerOptions options)
        {
            var repository = _repository ?? throw new InvalidOperationException("A type repository is required to deserialize embedded userdata.");
            // The embedded payload carries its own concrete type name; the outer field only
            // declares a base class. Prefer the inner `@type` when present.
            var embeddedTypeName = element.TryGetProperty("@type", out var embeddedTypeElement)
                ? embeddedTypeElement.GetString() ?? throw new InvalidOperationException("Missing @type value.")
                : typeName;
            var type = ResolveType(embeddedTypeName);
            var hash = element.TryGetProperty("@hash", out var hashElement) ? hashElement.GetInt32() : 0;

            var objects = ImmutableArray.CreateBuilder<RszObjectNode>();
            if (element.TryGetProperty("@objects", out var objectsElement) && objectsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var objectElement in objectsElement.EnumerateArray())
                {
                    var objectType = ResolveObjectType(objectElement, null);
                    objects.Add((RszObjectNode)ReadObjectNode(objectElement, objectType, options));
                }
            }

            if (objects.Count == 0)
                throw new InvalidOperationException("Embedded userdata JSON contains no objects.");

            var builder = new RszFile.Builder(repository, 8);
            builder.Objects = objects.ToImmutable();
            return new RszEmbeddedUserValueNode(type, hash, builder.Build());
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
                // Uri-typed fields hold guid values in RE2 (v16); JSON may use a bare
                // string or the { "@ref": ... } shape for GameObjectRef-style values.
                RszFieldType.Uri => typeof(Guid),
                // RE2 (v16) stores GameObjectRef guid values in Uri-typed fields too.
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
                // Unknown dump types round-trip as their raw little-endian bytes (base64 in JSON).
                RszFieldType.ukn_error => typeof(byte[]),
                _ => throw new NotSupportedException($"Unsupported RSZ value type '{type}'.")
            };
        }
    }
}
