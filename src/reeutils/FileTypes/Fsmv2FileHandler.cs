using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
            var tree = new BhvtFile(Version, Data).ReadTree(Repository);
            using var raw = JsonSerializer.SerializeToDocument(SerializeNode(tree), JsonSupport.CreateOptions());
            return JsonSupport.ApplyTreeOptions(raw, options);
        }

        public override byte[] Import(JsonDocument json)
        {
            var template = EmbeddedData.GetFile($"empty.fsmv2.{Version}") ?? throw new NotSupportedException($"No embedded template exists for .fsmv2.{Version}.");
            var builder = new BhvtFile(Version, template).ToBuilder(Repository);
            builder.Root = DeserializeNode(json.RootElement, Repository);
            return builder.Build().Data.ToArray();
        }

        private static JsonObject SerializeNode(BhvtNode node)
        {
            return new JsonObject
            {
                ["id"] = node.Id.Id,
                ["exId"] = node.Id.ExId,
                ["name"] = node.Name,
                ["attributes"] = node.Attributes.ToString(),
                ["priority"] = node.Priority,
                ["isBranch"] = node.IsBranch,
                ["isEnd"] = node.IsEnd,
                ["workFlags"] = node.WorkFlags.ToString(),
                ["nameHash"] = node.NameHash,
                ["fullNameHash"] = node.FullNameHash,
                ["tags"] = new JsonArray([.. node.Tags.Select(t => (JsonNode)t)]),
                ["selector"] = ToJsonNode(node.Selector),
                ["selectorCallerCondition"] = ToJsonNode(node.SelectorCallerCondition),
                ["selectorCallers"] = new JsonArray([.. node.SelectorCallers.Select(x => ToJsonNode(x))]),
                ["actions"] = new JsonArray([.. node.Actions.Select(a => (JsonNode)new JsonObject
                {
                    ["instance"] = ToJsonNode(a.Instance),
                    ["actionEx"] = a.ActionEx,
                })]),
                ["states"] = new JsonArray([.. node.States.Select(s => (JsonNode)new JsonObject
                {
                    ["target"] = NodeIdRef(s.Target),
                    ["condition"] = ToJsonNode(s.Condition),
                    ["transitionMapId"] = s.TransitionMapId,
                    ["stateEx"] = s.StateEx,
                    ["events"] = new JsonArray([.. s.Events.Select(x => ToJsonNode(x))]),
                })]),
                ["transitions"] = new JsonArray([.. node.Transitions.Select(t => (JsonNode)new JsonObject
                {
                    ["start"] = t.Start.IsUnset ? null : NodeIdRef(t.Start),
                    ["condition"] = ToJsonNode(t.Condition),
                })]),
                ["allStates"] = new JsonArray([.. node.AllStates.Select(s => (JsonNode)new JsonObject
                {
                    ["target"] = NodeIdRef(s.Target),
                    ["condition"] = ToJsonNode(s.Condition),
                    ["transitionMapId"] = s.TransitionMapId,
                    ["transitionAttributes"] = s.TransitionAttributes,
                })]),
                ["referenceTree"] = node.ReferenceTree,
                ["children"] = new JsonArray([.. node.Children.Select(c => (JsonNode)new JsonObject
                {
                    ["condition"] = ToJsonNode(c.Condition),
                    ["node"] = SerializeNode(c.Node),
                })]),
            };
        }

        private static JsonObject NodeIdRef(BhvtNodeId id) => new()
        {
            ["id"] = id.Id,
            ["exId"] = id.ExId,
        };

        private static JsonNode? ToJsonNode(RszObjectNode? instance)
        {
            if (instance == null) return null;
            var text = RszJsonSerializer.Serialize(instance, JsonSupport.CreateOptions());
            return JsonNode.Parse(text);
        }

        private static BhvtNode DeserializeNode(JsonElement element, RszTypeRepository repository)
        {
            RszObjectNode? ParseInstance(string propertyName)
            {
                if (!element.TryGetProperty(propertyName, out var e) || e.ValueKind == JsonValueKind.Null)
                    return null;
                using var document = JsonDocument.Parse(e.GetRawText());
                return (RszObjectNode)RszJsonSerializer.Deserialize(document, repository);
            }

            static RszObjectNode? ParseInstanceElement(JsonElement e, RszTypeRepository repository)
            {
                if (e.ValueKind == JsonValueKind.Null) return null;
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
                ? scEl.EnumerateArray().Select(x => ParseInstanceElement(x, repository)).Where(x => x != null).Select(x => x!).ToImmutableArray()
                : ImmutableArray<RszObjectNode>.Empty;

            var actions = element.TryGetProperty("actions", out var actionsEl) && actionsEl.ValueKind == JsonValueKind.Array
                ? actionsEl.EnumerateArray().Select(a => new BhvtAction(
                    ParseInstanceElement(a.GetProperty("instance"), repository) ?? throw new InvalidOperationException("Action is missing its instance."),
                    GetOrDefault(a, "actionEx", 0u, v => v.GetUInt32()))).ToImmutableArray()
                : ImmutableArray<BhvtAction>.Empty;

            var states = element.TryGetProperty("states", out var statesEl) && statesEl.ValueKind == JsonValueKind.Array
                ? statesEl.EnumerateArray().Select(s => new BhvtState(
                    ParseNodeIdRef(s, "target"),
                    ParseInstanceElement(s.GetProperty("condition"), repository),
                    GetOrDefault(s, "transitionMapId", 0u, v => v.GetUInt32()),
                    GetOrDefault(s, "stateEx", 0u, v => v.GetUInt32()),
                    s.TryGetProperty("events", out var evEl) && evEl.ValueKind == JsonValueKind.Array
                        ? evEl.EnumerateArray().Select(x => ParseInstanceElement(x, repository)).Where(x => x != null).Select(x => x!).ToImmutableArray()
                        : ImmutableArray<RszObjectNode>.Empty)).ToImmutableArray()
                : ImmutableArray<BhvtState>.Empty;

            var transitions = element.TryGetProperty("transitions", out var transEl) && transEl.ValueKind == JsonValueKind.Array
                ? transEl.EnumerateArray().Select(t => new BhvtTransition(
                    ParseNodeIdRef(t, "start"),
                    ParseInstanceElement(t.GetProperty("condition"), repository),
                    [])).ToImmutableArray()
                : ImmutableArray<BhvtTransition>.Empty;

            var allStates = element.TryGetProperty("allStates", out var allStatesEl) && allStatesEl.ValueKind == JsonValueKind.Array
                ? allStatesEl.EnumerateArray().Select(s => new BhvtAllState(
                    ParseNodeIdRef(s, "target"),
                    ParseInstanceElement(s.GetProperty("condition"), repository),
                    GetOrDefault(s, "transitionMapId", 0u, v => v.GetUInt32()),
                    GetOrDefault(s, "transitionAttributes", 0u, v => v.GetUInt32()))).ToImmutableArray()
                : ImmutableArray<BhvtAllState>.Empty;

            var children = element.TryGetProperty("children", out var childrenEl) && childrenEl.ValueKind == JsonValueKind.Array
                ? childrenEl.EnumerateArray().Select(c => new BhvtChild(
                    DeserializeNode(c.GetProperty("node"), repository),
                    ParseInstanceElement(c.GetProperty("condition"), repository))).ToImmutableArray()
                : ImmutableArray<BhvtChild>.Empty;

            return new BhvtNode(
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
        }
    }
}
