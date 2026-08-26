using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class Fsmv2FileHandler(string path, byte[] data, int version, RszTypeRepository? repository)
        : RszFileHandlerBase(path, data, version, repository)
    {
        public override Dictionary<string, object?> GetSummary()
        {
            var file = new BhvtFile(Version, Data);
            var summary = CreateSummary("FSMV2");
            summary["Version"] = file.Version;
            summary["RSZ version"] = file.RszVersion;
            summary["Instances"] = file.InstanceCount;
            summary["Resources"] = file.Resources.Length;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            var file = new BhvtFile(Version, Data);

            // Decode the static tables ONCE and share the instances between ReadTree and the
            // table index: objects referenced from the tree that live in a static table are then
            // emitted as {"$tbl": "...", "i": n} refs, so import can reuse the exact seeded
            // instances (table membership is identity-based).
            var staticActions = TryReadTable(file.StaticActionRsz, Repository, out var sa) ? sa!.Value : [];
            var staticSelectorCallers = TryReadTable(file.StaticSelectorCallerRsz, Repository, out var ssc) ? ssc!.Value : [];
            var staticConditions = TryReadTable(file.StaticConditionsRsz, Repository, out var sc) ? sc!.Value : [];
            var staticTransitionEvents = TryReadTable(file.StaticTransitionEventRsz, Repository, out var ste) ? ste!.Value : [];

            // Same treatment for the five dynamic tables: decode ONCE so tree references and the
            // table index share instances; import then reuses the seeded objects instead of
            // appending fresh deserialized copies (which would grow the streams and shift headers).
            var actions = file.ActionRsz.ReadObjectList(Repository);
            var selectors = file.SelectorRsz.ReadObjectList(Repository);
            var selectorCallers = file.SelectorCallerRsz.ReadObjectList(Repository);
            var conditions = file.ConditionsRsz.ReadObjectList(Repository);
            var transitionEvents = file.TransitionEventRsz.ReadObjectList(Repository);

            var tree = file.ReadTree(Repository,
                staticActions, staticSelectorCallers, staticConditions, staticTransitionEvents,
                actions, selectors, selectorCallers, conditions, transitionEvents);

            var staticIndex = new StaticTableIndex();
            staticIndex.Add("staticActions", staticActions);
            staticIndex.Add("staticSelectorCallers", staticSelectorCallers);
            staticIndex.Add("staticConditions", staticConditions);
            staticIndex.Add("staticTransitionEvents", staticTransitionEvents);
            staticIndex.Add("actions", actions);
            staticIndex.Add("selectors", selectors);
            staticIndex.Add("selectorCallers", selectorCallers);
            staticIndex.Add("conditions", conditions);
            staticIndex.Add("transitionEvents", transitionEvents);

            using var raw = JsonSerializer.SerializeToDocument(SerializeNode(tree, staticIndex), JsonSupport.CreateOptions());
            var json = JsonSupport.ApplyTreeOptions(raw, options);

            // The tree alone can't restore the file's non-tree state (header hash, embedded UVar
            // blob, prefab-gameobject references, expression-tree condition tables); carry them as
            // side-data.
            var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("@meta-bhvt");
                writer.WriteStartObject();
                writer.WriteNumber("hash", file.Hash);
                WriteRszStream(writer, "actions", file.ActionRsz);
                WriteRszStream(writer, "selectors", file.SelectorRsz);
                WriteRszStream(writer, "selectorCallers", file.SelectorCallerRsz);
                WriteRszStream(writer, "conditions", file.ConditionsRsz);
                WriteRszStream(writer, "transitionEvents", file.TransitionEventRsz);
                WriteRszStream(writer, "staticActions", file.StaticActionRsz);
                WriteRszStream(writer, "staticSelectorCallers", file.StaticSelectorCallerRsz);
                WriteRszStream(writer, "staticConditions", file.StaticConditionsRsz);
                WriteRszStream(writer, "staticTransitionEvents", file.StaticTransitionEventRsz);
                WriteRszStream(writer, "expressionTreeConditions", file.ExpressionTreeConditionsRsz);
                WriteRszStream(writer, "staticExpressionTreeConditions", file.StaticExpressionTreeConditionsRsz);
                writer.WritePropertyName("uvar");
                writer.WriteStartObject();
                writer.WriteString("blob", Convert.ToBase64String(file.Data.Span[(int)file.UvarOffset..]));
                writer.WriteNumber("originalOffset", file.UvarOffset);
                writer.WriteNumber("originalBaseVariableOffset", file.BaseVariableOffset);
                writer.WriteEndObject();
                if (!file.GameObjectReferences.IsEmpty)
                {
                    writer.WritePropertyName("gameObjectReferences");
                    writer.WriteStartArray();
                    foreach (var g in file.GameObjectReferences)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("guid", g.Guid.ToString());
                        writer.WriteStartArray("values");
                        foreach (var v in g.Values) writer.WriteNumberValue(v);
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                foreach (var property in json.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            ms.Position = 0;
            return JsonDocument.Parse(ms);
        }

        private static bool TryReadTable(RszFile rsz, RszTypeRepository repository, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ImmutableArray<RszObjectNode>? nodes)
        {
            try
            {
                nodes = rsz.ReadObjectList(repository);
                return true;
            }
            catch (Exception)
            {
                nodes = null;
                return false;
            }
        }

        public override byte[] Import(JsonDocument json)
        {
            var builder = CreateBuilder();
            if (json.RootElement.TryGetProperty("@meta-bhvt", out var meta) &&
                meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("hash", out var hashEl))
                    builder.Hash = hashEl.GetUInt32();

                // Seed the dynamic tables FIRST, then the statics: tree refs resolve back to these
                // very instances (identity-based), and seeding preserves source table order so
                // rebuilds stay byte-identical even though the tree walk discovers objects in a
                // different sequence.
                SeedDynamicTables(builder, meta, Repository);
                SeedStaticTable(builder.AddStaticAction, meta, "staticActions", Repository);
                SeedStaticTable(builder.AddStaticSelectorCaller, meta, "staticSelectorCallers", Repository);
                SeedStaticTable(builder.AddStaticCondition, meta, "staticConditions", Repository);
                SeedStaticTable(builder.AddStaticTransitionEvent, meta, "staticTransitionEvents", Repository);

                if (TryReadStream(meta, "expressionTreeConditions", out var expressionTreeConditions))
                    builder.ExpressionTreeConditions = expressionTreeConditions.ReadObjectList(Repository);
                if (TryReadStream(meta, "staticExpressionTreeConditions", out var staticExpressionTreeConditions))
                    builder.StaticExpressionTreeConditions = staticExpressionTreeConditions.ReadObjectList(Repository);
                if (meta.TryGetProperty("uvar", out var uvar) && uvar.ValueKind == JsonValueKind.Object &&
                    uvar.TryGetProperty("blob", out var blobEl) && blobEl.ValueKind == JsonValueKind.String)
                {
                    builder.UvarBlob = Convert.FromBase64String(blobEl.GetString()!);
                    builder.UvarBlobOriginalOffset =
                        uvar.TryGetProperty("originalOffset", out var ooEl) ? ooEl.GetInt64() : 0;
                    builder.UvarBlobOriginalBaseVariableOffset =
                        uvar.TryGetProperty("originalBaseVariableOffset", out var obvoEl) ? obvoEl.GetInt64() : 0;
                }
                if (meta.TryGetProperty("gameObjectReferences", out var gosEl) && gosEl.ValueKind == JsonValueKind.Array)
                {
                    var refs = ImmutableArray.CreateBuilder<BhvtGameObjectReference>();
                    foreach (var goEl in gosEl.EnumerateArray())
                    {
                        var values = goEl.GetProperty("values").EnumerateArray().Select(v => v.GetInt32()).ToArray();
                        refs.Add(new BhvtGameObjectReference(Guid.Parse(goEl.GetProperty("guid").GetString()!), [.. values]));
                    }
                    builder.GameObjectReferences = refs.ToImmutable();
                }
            }
            builder.Root = DeserializeNode(json.RootElement, Repository, builder);
            return builder.Build().Data.ToArray();
        }

        private BhvtFile.Builder CreateBuilder()
        {
            var template = EmbeddedData.GetFile($"empty.fsmv2.{Version}");
            if (template != null)
                return new BhvtFile(Version, template).ToBuilder(Repository);
            if (TryGetRszVersion(out var rszVersion))
                return new BhvtFile.Builder(Repository, Version, rszVersion);
            throw new NotSupportedException($"No embedded template exists for .fsmv2.{Version}.");
        }

        private static void WriteRszStream(Utf8JsonWriter writer, string name, RszFile rsz)
        {
            writer.WritePropertyName(name);
            writer.WriteStringValue(Convert.ToBase64String(rsz.Data.Span));
        }

        private static void SeedStaticTable(Action<RszObjectNode> add, JsonElement meta, string name, RszTypeRepository repository)
        {
            if (TryReadStream(meta, name, out var rsz))
            {
                foreach (var node in rsz.ReadObjectList(repository))
                {
                    add(node);
                }
            }
        }

        private static void SeedDynamicTables(BhvtFile.Builder builder, JsonElement meta, RszTypeRepository repository)
        {
            if (!meta.TryGetProperty("actions", out _)) return;
            TryReadStream(meta, "actions", out var actions);
            TryReadStream(meta, "selectors", out var selectors);
            TryReadStream(meta, "selectorCallers", out var selectorCallers);
            TryReadStream(meta, "conditions", out var conditions);
            TryReadStream(meta, "transitionEvents", out var transitionEvents);
            builder.SeedDynamicTables(
                actions.ReadObjectList(repository),
                selectors.ReadObjectList(repository),
                selectorCallers.ReadObjectList(repository),
                conditions.ReadObjectList(repository),
                transitionEvents.ReadObjectList(repository));
        }

        private static bool TryReadStream(JsonElement meta, string name, out RszFile rsz)
        {
            rsz = default!;
            if (!meta.TryGetProperty(name, out var e) || e.ValueKind != JsonValueKind.String)
                return false;
            var bytes = Convert.FromBase64String(e.GetString()!);
            rsz = new RszFile(bytes);
            return true;
        }

        private bool TryGetRszVersion(out int rszVersion)
        {
            // fsmv2.30 is RE2/RE3 (non-RT); the behaviour file RSZ stream is always version 8.
            rszVersion = Version == 30 ? 8 : 0;
            return rszVersion != 0;
        }

        /// <summary>
        /// Lets the JSON exporter emit <c>{"$tbl": "...", "i": n}</c> references for objects that
        /// live in a static table instead of inlining them. Import resolves these back to the very
        /// same decoded instances that seeded the builder, which keeps the rebuild byte-identical
        /// (table membership is by object identity).
        /// </summary>
        private sealed class StaticTableIndex
        {
            private readonly Dictionary<RszObjectNode, (string Table, int Index)> _map = new();

            public static readonly string[] Tables =
            [
                "staticActions",
                "staticSelectorCallers",
                "staticConditions",
                "staticTransitionEvents",
                "actions",
                "selectors",
                "selectorCallers",
                "conditions",
                "transitionEvents",
            ];

            public void Add(string table, IReadOnlyList<RszObjectNode> nodes)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    _map.TryAdd(nodes[i], (table, i));
                }
            }

            public bool TryGetRef(RszObjectNode node, out string table, out int index)
            {
                if (_map.TryGetValue(node, out var hit))
                {
                    (table, index) = hit;
                    return true;
                }
                table = "";
                index = -1;
                return false;
            }
        }

        private JsonObject SerializeNode(BhvtNode node, StaticTableIndex staticIndex)
        {
            return new JsonObject
            {
                ["id"] = node.Id.Id,
                ["exId"] = node.Id.ExId,
                ["name"] = node.Name,
                ["originalTableIndex"] = node.OriginalTableIndex,
                ["attributes"] = node.Attributes.ToString(),
                ["priority"] = node.Priority,
                ["isBranch"] = node.IsBranch,
                ["isEnd"] = node.IsEnd,
                ["workFlags"] = node.WorkFlags.ToString(),
                ["nameHash"] = node.NameHash,
                ["fullNameHash"] = node.FullNameHash,
                ["tags"] = new JsonArray([.. node.Tags.Select(t => (JsonNode)t)]),
                ["selector"] = ToJsonNode(node.Selector, staticIndex),
                ["selectorCallerCondition"] = ToJsonNode(node.SelectorCallerCondition, staticIndex),
                ["selectorCallers"] = new JsonArray([.. node.SelectorCallers.Select(x => ToJsonNode(x, staticIndex))]),
                ["actions"] = new JsonArray([.. node.Actions.Select(a => (JsonNode)new JsonObject
                {
                    ["instance"] = ToJsonNode(a.Instance, staticIndex),
                    ["actionEx"] = a.ActionEx,
                })]),
                // Raw slot words from the source file: some ids don't resolve to a decoded action
                // object, so the resolved model alone can't reproduce them. Exported only when
                // they line up with the model (same guard Build() applies when writing).
                ["rawActionIds"] = RawSlotIdsOrNull(node),
                ["states"] = new JsonArray([.. node.States.Select(s => (JsonNode)new JsonObject
                {
                    ["target"] = NodeIdRef(s.Target),
                    ["condition"] = ToJsonNode(s.Condition, staticIndex),
                    ["transitionMapId"] = s.TransitionMapId,
                    ["stateEx"] = s.StateEx,
                    ["events"] = new JsonArray([.. s.Events.Select(x => ToJsonNode(x, staticIndex))]),
                    ["rawEventIds"] = RawEventIdsOrNull(s),
                })]),
                ["transitions"] = new JsonArray([.. node.Transitions.Select(t => (JsonNode)new JsonObject
                {
                    ["start"] = t.Start.IsUnset ? null : NodeIdRef(t.Start),
                    ["condition"] = ToJsonNode(t.Condition, staticIndex),
                })]),
                ["allStates"] = new JsonArray([.. node.AllStates.Select(s => (JsonNode)new JsonObject
                {
                    ["target"] = NodeIdRef(s.Target),
                    ["condition"] = ToJsonNode(s.Condition, staticIndex),
                    ["transitionMapId"] = s.TransitionMapId,
                    ["transitionAttributes"] = s.TransitionAttributes,
                })]),
                ["referenceTree"] = node.ReferenceTree,
                ["children"] = new JsonArray([.. node.Children.Select(c => (JsonNode)new JsonObject
                {
                    ["condition"] = ToJsonNode(c.Condition, staticIndex),
                    ["node"] = SerializeNode(c.Node, staticIndex),
                })]),
            };
        }

        private static JsonArray? RawSlotIdsOrNull(BhvtNode node)
        {
            var raw = node.RawActionSlots;
            if (raw == null || raw.Value.Length != node.Actions.Length)
                return null;
            return new JsonArray([.. raw.Value.Select(x => (JsonNode)JsonValue.Create(x.Action))]);
        }

        private static JsonArray? RawEventIdsOrNull(BhvtState state)
        {
            var raw = state.RawEventIds;
            if (raw == null)
                return null;
            return new JsonArray([.. raw.Value.Select(x => (JsonNode)JsonValue.Create(x))]);
        }

        private static JsonObject NodeIdRef(BhvtNodeId id) => new()
        {
            ["id"] = id.Id,
            ["exId"] = id.ExId,
        };

        private JsonNode? ToJsonNode(RszObjectNode? instance, StaticTableIndex staticIndex)
        {
            if (instance == null) return null;
            if (staticIndex.TryGetRef(instance, out var table, out var index))
            {
                return new JsonObject { ["$tbl"] = table, ["i"] = index };
            }
            // Repository-aware serialization: embedded userdata needs the type repository to
            // emit its nested JSON; without it the serializer throws.
            var text = RszJsonSerializer.Serialize(instance, Repository!, JsonSupport.CreateOptions());
            return JsonNode.Parse(text);
        }

        private BhvtNode DeserializeNode(JsonElement element, RszTypeRepository repository, BhvtFile.Builder? builder)
        {
            RszObjectNode? ParseInstance(string propertyName)
            {
                if (!element.TryGetProperty(propertyName, out var e) || e.ValueKind == JsonValueKind.Null)
                    return null;
                return ParseInstanceElement(e, repository, builder);
            }

            static RszObjectNode? ParseInstanceElement(JsonElement e, RszTypeRepository repository, BhvtFile.Builder? builder)
            {
                if (e.ValueKind == JsonValueKind.Null) return null;
                if (e.ValueKind == JsonValueKind.Object &&
                    e.TryGetProperty("$tbl", out var tblEl))
                {
                    var resolved = builder?.ResolveStaticRef(
                        tblEl.GetString()!, e.GetProperty("i").GetInt32());
                    if (resolved != null)
                        return resolved;
                }
                using var document = JsonDocument.Parse(e.GetRawText());
                return (RszObjectNode)RszJsonSerializer.Deserialize(document, repository);
            }

            static BhvtNodeId ParseNodeId(JsonElement e) => new(
                e.GetProperty("id").GetUInt32(),
                e.TryGetProperty("exId", out var exIdEl) && exIdEl.ValueKind != JsonValueKind.Null ? exIdEl.GetUInt32() : 0);

            static BhvtNodeId ParseNodeIdRef(JsonElement parent, string propertyName) =>
                parent.TryGetProperty(propertyName, out var e) && e.ValueKind != JsonValueKind.Null
                    ? ParseNodeId(e)
                    : BhvtNodeId.Unset;

            static T GetOrDefault<T>(JsonElement e, string name, T fallback, Func<JsonElement, T> get) =>
                e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? get(v) : fallback;

            var attributes = GetOrDefault(element, "attributes", BhvtNodeAttributes.None,
                v => Enum.TryParse<BhvtNodeAttributes>(v.GetString(), out var a) ? a : BhvtNodeAttributes.None);
            var workFlags = GetOrDefault(element, "workFlags", BhvtWorkFlags.None,
                v => Enum.TryParse<BhvtWorkFlags>(v.GetString(), out var a) ? a : BhvtWorkFlags.None);

            var tags = element.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                ? tagsEl.EnumerateArray().Select(x => x.GetUInt32()).ToImmutableArray()
                : [];

            var selectorCallers = element.TryGetProperty("selectorCallers", out var scEl) && scEl.ValueKind == JsonValueKind.Array
                ? scEl.EnumerateArray().Select(x => ParseInstanceElement(x, repository, builder)).Where(x => x != null).Select(x => x!).ToImmutableArray()
                : ImmutableArray<RszObjectNode>.Empty;

            var actions = element.TryGetProperty("actions", out var actionsEl) && actionsEl.ValueKind == JsonValueKind.Array
                ? actionsEl.EnumerateArray().Select(a => new BhvtAction(
                    ParseInstanceElement(a.GetProperty("instance"), repository, builder) ?? throw new InvalidOperationException("Action is missing its instance."),
                    GetOrDefault(a, "actionEx", 0u, v => v.GetUInt32()))).ToImmutableArray()
                : ImmutableArray<BhvtAction>.Empty;
            var rawActionIds = ParseRawWordArray(element, "rawActionIds");
            var rawExIds = ParseRawWordArray(element, "rawActionExs");

            var states = element.TryGetProperty("states", out var statesEl) && statesEl.ValueKind == JsonValueKind.Array
                ? statesEl.EnumerateArray().Select(s => new BhvtState(
                    ParseNodeIdRef(s, "target"),
                    ParseInstanceElement(s.GetProperty("condition"), repository, builder),
                    GetOrDefault(s, "transitionMapId", 0u, v => v.GetUInt32()),
                    GetOrDefault(s, "stateEx", 0u, v => v.GetUInt32()),
                    s.TryGetProperty("events", out var evEl) && evEl.ValueKind == JsonValueKind.Array
                        ? evEl.EnumerateArray().Select(x => ParseInstanceElement(x, repository, builder)).Where(x => x != null).Select(x => x!).ToImmutableArray()
                        : ImmutableArray<RszObjectNode>.Empty)
                {
                    // Raw words survive the JSON hop so Build() writes the source's exact slot
                    // values instead of recomputing ids from decoded objects.
                    RawEventIds = ParseRawWordArray(s, "rawEventIds"),
                }).ToImmutableArray()
                : ImmutableArray<BhvtState>.Empty;

            var transitions = element.TryGetProperty("transitions", out var transEl) && transEl.ValueKind == JsonValueKind.Array
                ? transEl.EnumerateArray().Select(t => new BhvtTransition(
                    ParseNodeIdRef(t, "start"),
                    ParseInstanceElement(t.GetProperty("condition"), repository, builder),
                    [])).ToImmutableArray()
                : ImmutableArray<BhvtTransition>.Empty;

            var allStates = element.TryGetProperty("allStates", out var allStatesEl) && allStatesEl.ValueKind == JsonValueKind.Array
                ? allStatesEl.EnumerateArray().Select(s => new BhvtAllState(
                    ParseNodeIdRef(s, "target"),
                    ParseInstanceElement(s.GetProperty("condition"), repository, builder),
                    GetOrDefault(s, "transitionMapId", 0u, v => v.GetUInt32()),
                    GetOrDefault(s, "transitionAttributes", 0u, v => v.GetUInt32()))).ToImmutableArray()
                : ImmutableArray<BhvtAllState>.Empty;

            var children = element.TryGetProperty("children", out var childrenEl) && childrenEl.ValueKind == JsonValueKind.Array
                ? childrenEl.EnumerateArray().Select(c => new BhvtChild(
                    DeserializeNode(c.GetProperty("node"), repository, builder),
                    ParseInstanceElement(c.GetProperty("condition"), repository, builder))).ToImmutableArray()
                : ImmutableArray<BhvtChild>.Empty;

            var node = new BhvtNode(
                ParseNodeId(element),
                element.GetProperty("name").GetString() ?? "",
                attributes,
                GetOrDefault(element, "priority", 0, v => v.GetInt32()),
                GetOrDefault(element, "isBranch", false, v => v.GetBoolean()),
                GetOrDefault(element, "isEnd", false, v => v.GetBoolean()),
                workFlags,
                GetOrDefault(element, "nameHash", 0u, v => v.GetUInt32()),
                GetOrDefault(element, "fullNameHash", 0u, v => v.GetUInt32()),
                tags,
                ParseInstance("selector"),
                ParseInstance("selectorCallerCondition"),
                selectorCallers,
                actions,
                children,
                states,
                transitions,
                allStates,
                element.TryGetProperty("referenceTree", out var rtEl) && rtEl.ValueKind == JsonValueKind.String ? rtEl.GetString() : null);

            // Carry the source node-table position through the JSON hop so Build()'s
            // order-restore fires; hand-authored JSON without this key keeps -1 (pre-order).
            node.OriginalTableIndex = GetOrDefault(element, "originalTableIndex", -1, v => v.GetInt32());

            if (rawActionIds != null)
            {
                // Only attach when the words line up with the decoded model; otherwise Build()
                // falls back to deriving ids from resolved instances (its length-guard).
                if (rawActionIds.Value.Length == actions.Length)
                {
                    var slots = ImmutableArray.CreateBuilder<(uint Action, uint ActionEx)>(actions.Length);
                    for (var i = 0; i < actions.Length; i++)
                    {
                        var ex = rawExIds != null && rawExIds.Value.Length == actions.Length ? rawExIds.Value[i] : actions[i].ActionEx;
                        slots.Add((rawActionIds.Value[i], ex));
                    }
                    node.RawActionSlots = slots.MoveToImmutable();
                }
            }
            return node;
        }

        private static ImmutableArray<uint>? ParseRawWordArray(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Array)
                return null;
            var builder = ImmutableArray.CreateBuilder<uint>();
            foreach (var item in el.EnumerateArray())
                builder.Add(item.GetUInt32());
            return builder.Count > 0 ? builder.ToImmutable() : null;
        }
    }
}
