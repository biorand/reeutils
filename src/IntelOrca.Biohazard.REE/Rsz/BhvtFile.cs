using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.REE.Extensions;
using IntelOrca.Biohazard.REE.Variables;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// Behavior tree (BHVT) files: used directly by .bhvt AI trees and, with the FSM-node layout
    /// implemented here, by .fsmv2 state machine files. Version 40 is used by Resident Evil Village (RE8).
    /// </summary>
    /// <remarks>
    /// The alternate "AI" node layout used by plain .bhvt files (no FSM attributes, no states/transitions)
    /// is not implemented here; this type only supports the FSM-node layout used by .fsmv2 files.
    /// </remarks>
    public sealed class BhvtFile(int version, ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x54564842; // "BHVT"
        internal const int ActionIdFieldIndex = 1;

        public ReadOnlyMemory<byte> Data => data;
        public int Version => version;

        private BhvtHeader Header => new(Version, data);

        public uint Magic => Header.Magic;
        public uint Hash => Header.Hash;
        public int RszVersion => ActionRsz.Version;

        public RszFile ActionRsz => new(data.Slice((int)Header.ActionOffset));
        public RszFile SelectorRsz => new(data.Slice((int)Header.SelectorOffset));
        public RszFile SelectorCallerRsz => new(data.Slice((int)Header.SelectorCallerOffset));
        public RszFile ConditionsRsz => new(data.Slice((int)Header.ConditionsOffset));
        public RszFile TransitionEventRsz => new(data.Slice((int)Header.TransitionEventOffset));
        public RszFile ExpressionTreeConditionsRsz => new(data.Slice((int)Header.ExpressionTreeConditionsOffset));
        public RszFile StaticActionRsz => new(data.Slice((int)Header.StaticActionOffset));
        public RszFile StaticSelectorCallerRsz => new(data.Slice((int)Header.StaticSelectorCallerOffset));
        public RszFile StaticConditionsRsz => new(data.Slice((int)Header.StaticConditionsOffset));
        public RszFile StaticTransitionEventRsz => new(data.Slice((int)Header.StaticTransitionEventOffset));
        public RszFile StaticExpressionTreeConditionsRsz => new(data.Slice((int)Header.StaticExpressionTreeConditionsOffset));

        public int InstanceCount => ActionRsz.InstanceCount;

        /// <summary>
        /// Combined resource-dependency pool: node reference-tree paths followed by any resource paths
        /// referenced from the embedded RSZ streams. Unlike .pfb/.scn, entries aren't individually
        /// addressable by offset -- they're a flat, sequential, declarative dependency list.
        /// </summary>
        public ImmutableArray<string> Resources => ReadStringPool((int)Header.ResourcePathsOffset, hasCount: true);

        public UvarFile UserVariables => new(data.Slice((int)Header.VariableOffset));

        public ImmutableArray<UvarFile> SubVariables
        {
            get
            {
                var offset = (int)Header.BaseVariableOffset;
                var count = ReadInt32(offset);
                if (count <= 0) return [];

                var result = ImmutableArray.CreateBuilder<UvarFile>(count);
                for (var i = 0; i < count; i++)
                {
                    var uvarOffset = ReadInt64(offset + 4 + i * 8);
                    result.Add(new UvarFile(data.Slice((int)uvarOffset)));
                }
                return result.ToImmutable();
            }
        }

        public ImmutableArray<BhvtGameObjectReference> GameObjectReferences
        {
            get
            {
                var offset = (int)Header.ReferencePrefabGameObjectsOffset;
                if (offset <= 0 || offset >= data.Length) return [];

                var reader = new BhvtReader(data, offset);
                var count = reader.ReadInt32();
                if (count <= 0) return [];

                var result = ImmutableArray.CreateBuilder<BhvtGameObjectReference>(count);
                for (var i = 0; i < count; i++)
                {
                    var guid = reader.ReadGuid();
                    var valueCount = reader.ReadInt32();
                    result.Add(new BhvtGameObjectReference(guid, reader.ReadInt32List(valueCount)));
                }
                return result.ToImmutable();
            }
        }

        /// <summary>
        /// Reads the full node tree, with actions/conditions/selectors resolved into their decoded RSZ
        /// object instances. Requires a type repository for the file's game.
        /// </summary>
        public BhvtNode ReadTree(RszTypeRepository repository)
        {
            var actionObjects = ActionRsz.ReadObjectList(repository);
            var staticActionObjects = StaticActionRsz.ReadObjectList(repository);
            var selectorObjects = SelectorRsz.ReadObjectList(repository);
            var selectorCallerObjects = SelectorCallerRsz.ReadObjectList(repository);
            var staticSelectorCallerObjects = StaticSelectorCallerRsz.ReadObjectList(repository);
            var conditionObjects = ConditionsRsz.ReadObjectList(repository);
            var staticConditionObjects = StaticConditionsRsz.ReadObjectList(repository);
            var transitionEventObjects = TransitionEventRsz.ReadObjectList(repository);
            var staticTransitionEventObjects = StaticTransitionEventRsz.ReadObjectList(repository);

            RszObjectNode? ResolveCondition(RawId id) => id.HasValue
                ? GetByIndex(id.IsStatic ? staticConditionObjects : conditionObjects, id.Index)
                : null;
            RszObjectNode? ResolveTransitionEvent(RawId id) => id.HasValue
                ? GetByIndex(id.IsStatic ? staticTransitionEventObjects : transitionEventObjects, id.Index)
                : null;
            RszObjectNode? ResolveSelectorCaller(RawId id) => id.HasValue
                ? GetByIndex(id.IsStatic ? staticSelectorCallerObjects : selectorCallerObjects, id.Index)
                : null;

            var rszVersion = RszVersion;
            var rawNodes = ReadRawNodes(rszVersion);
            var byId = rawNodes.ToDictionary(n => n.Id.Packed);

            // Actions are matched by content (their own id field), not table position, and matching is
            // order-dependent when ids repeat -- replay the same order (flat node-table order) the
            // format's other tooling uses, so ambiguous cases resolve the same way.
            var actionsById = ResolveActions(rawNodes, actionObjects, staticActionObjects);

            var built = new Dictionary<ulong, BhvtNode>();
            BhvtNode Build(RawNode raw)
            {
                if (built.TryGetValue(raw.Id.Packed, out var existing))
                    return existing;

                var children = ImmutableArray.CreateBuilder<BhvtChild>(raw.Children.Length);
                foreach (var (childId, conditionId) in raw.Children)
                {
                    if (!byId.TryGetValue(childId.Packed, out var childRaw))
                        throw new InvalidDataException($"BHVT node {raw.Id} references missing child {childId}.");
                    children.Add(new BhvtChild(Build(childRaw), ResolveCondition(conditionId)));
                }

                var states = raw.States.Select(s => new BhvtState(
                    s.Target,
                    ResolveCondition(s.ConditionId),
                    s.TransitionMapId,
                    s.StateEx,
                    [.. s.EventIds.Select(ResolveTransitionEvent).Where(x => x != null)!])).ToImmutableArray();

                var transitions = raw.Transitions.Select(t => new BhvtTransition(
                    t.Start,
                    ResolveCondition(t.ConditionId),
                    t.RawEvents)).ToImmutableArray();

                var allStates = raw.AllStates.Select(s => new BhvtAllState(
                    s.Target,
                    ResolveCondition(s.ConditionId),
                    s.TransitionMapId,
                    s.TransitionAttributes)).ToImmutableArray();

                var node = new BhvtNode(
                    raw.Id,
                    raw.Name,
                    raw.Attributes,
                    raw.Priority,
                    raw.IsBranch,
                    raw.IsEnd,
                    raw.WorkFlags,
                    raw.NameHash,
                    raw.FullNameHash,
                    raw.Tags,
                    GetByIndex(selectorObjects, raw.SelectorId),
                    ResolveCondition(raw.SelectorCallerConditionId),
                    [.. raw.SelectorCallerIds.Select(ResolveSelectorCaller).Where(x => x != null)!],
                    actionsById.TryGetValue(raw.Id.Packed, out var actions) ? actions : [],
                    children.ToImmutable(),
                    states,
                    transitions,
                    allStates,
                    raw.ReferenceTree);

                built[raw.Id.Packed] = node;
                return node;
            }

            var root = rawNodes.FirstOrDefault(n => n.ParentId.IsUnset)
                ?? throw new InvalidDataException("BHVT file has no root node (no node with an unset parent id).");
            var rootNode = Build(root);

            if (built.Count != rawNodes.Length)
            {
                throw new InvalidDataException(
                    $"BHVT file has {rawNodes.Length - built.Count} node(s) unreachable from the root; this isn't supported.");
            }

            return rootNode;
        }

        public Builder ToBuilder(RszTypeRepository repository) => new(repository, this);

        public sealed class Builder
        {
            public RszTypeRepository Repository { get; }
            public int Version { get; }
            public int RszVersion { get; }
            public BhvtNode Root { get; set; }

            /// <summary>
            /// Objects referenced only from embedded UVar expression trees (not modeled here), preserved
            /// as-is from the source file.
            /// </summary>
            public ImmutableArray<RszObjectNode> ExpressionTreeConditions { get; set; } = [];
            public ImmutableArray<RszObjectNode> StaticExpressionTreeConditions { get; set; } = [];

            public ImmutableArray<BhvtGameObjectReference> GameObjectReferences { get; set; } = [];

            /// <summary>Extra resource-dependency paths to declare beyond what's scanned automatically.</summary>
            public List<string> ExtraResources { get; } = [];

            /// <summary>
            /// Raw bytes of the embedded UVar section (main variables + sub-variable trees, through end of
            /// file), preserved verbatim from the source file and re-embedded with rebased internal
            /// offsets. UVar's own offsets are absolute-to-container when embedded (unlike RSZ, which is
            /// self-relative), which <see cref="Variables.UvarFile"/> isn't built to produce, so UVar
            /// content can't be edited through this API yet -- only carried through unchanged.
            /// </summary>
            public byte[]? UvarBlob { get; set; }

            /// <summary>Absolute position UvarBlob's internal offsets were originally computed against.</summary>
            public long UvarBlobOriginalOffset { get; set; }

            /// <summary>Where the sub-variable-tree table originally sat, in the same coordinate space as <see cref="UvarBlobOriginalOffset"/>.</summary>
            public long UvarBlobOriginalBaseVariableOffset { get; set; }

            public Builder(RszTypeRepository repository, int version, int rszVersion)
            {
                Repository = repository;
                Version = version;
                RszVersion = rszVersion;
                Root = new BhvtNode(
                    new BhvtNodeId(0, 0), "root",
                    BhvtNodeAttributes.IsEnabled | BhvtNodeAttributes.IsRestartable | BhvtNodeAttributes.IsFsmNode,
                    0, false, false, BhvtWorkFlags.None, 0, 0, [],
                    null, null, [], [], [], [], [], [], null);
            }

            public Builder(RszTypeRepository repository, BhvtFile instance)
            {
                Repository = repository;
                Version = instance.Version;
                RszVersion = instance.RszVersion;
                Root = instance.ReadTree(repository);
                ExpressionTreeConditions = instance.ExpressionTreeConditionsRsz.ReadObjectList(repository);
                StaticExpressionTreeConditions = instance.StaticExpressionTreeConditionsRsz.ReadObjectList(repository);
                GameObjectReferences = instance.GameObjectReferences;
                UvarBlobOriginalOffset = instance.Header.VariableOffset;
                UvarBlobOriginalBaseVariableOffset = instance.Header.BaseVariableOffset;
                UvarBlob = instance.Data.Slice((int)UvarBlobOriginalOffset).ToArray();
            }

            public BhvtFile Build()
            {
                // Flatten the tree (pre-order); node table order doesn't need to match the original, only
                // needs to be internally consistent.
                var nodes = new List<BhvtNode>();
                var parentOf = new Dictionary<BhvtNode, BhvtNode?>();
                void Flatten(BhvtNode node, BhvtNode? parent)
                {
                    nodes.Add(node);
                    parentOf[node] = parent;
                    foreach (var child in node.Children) Flatten(child.Node, node);
                }
                Flatten(Root, null);

                var seenIds = new HashSet<ulong>();
                foreach (var n in nodes)
                {
                    if (!seenIds.Add(n.Id.Packed))
                        throw new InvalidDataException($"Duplicate node id {n.Id} in the tree; every node needs a unique id.");
                }

                var selectorObjects = new List<RszObjectNode>();
                var actionObjects = new List<RszObjectNode>();
                var staticActionObjects = new List<RszObjectNode>();
                var selectorCallerObjects = new List<RszObjectNode>();
                var staticSelectorCallerObjects = new List<RszObjectNode>();
                var conditionObjects = new List<RszObjectNode>();
                var staticConditionObjects = new List<RszObjectNode>();
                var transitionEventObjects = new List<RszObjectNode>();
                var staticTransitionEventObjects = new List<RszObjectNode>();

                // A "static" table entry is simply one that already lived there when read; brand new
                // objects default to the regular table -- always valid, since idType is read explicitly
                // rather than inferred, "static" is just a size-saving convention for common built-ins.
                // Relies on RszObjectNode not overriding Equals (List.IndexOf falls back to reference
                // equality), so the same decoded instance is only ever added once.
                static (ushort index, bool isStatic) FindOrAdd(RszObjectNode obj, List<RszObjectNode> staticList, List<RszObjectNode> dynamicList)
                {
                    var si = staticList.IndexOf(obj);
                    if (si >= 0) return ((ushort)si, true);
                    var di = dynamicList.IndexOf(obj);
                    if (di >= 0) return ((ushort)di, false);
                    dynamicList.Add(obj);
                    return ((ushort)(dynamicList.Count - 1), false);
                }

                uint ToRawId((ushort index, bool isStatic) id) => id.index | ((uint)(id.isStatic ? 64 : 0) << 24);
                uint ToRawIdOrUnset((ushort index, bool isStatic)? id) => id.HasValue ? ToRawId(id.Value) : uint.MaxValue;

                (ushort, bool)? CollectCondition(RszObjectNode? condition) =>
                    condition == null ? null : FindOrAdd(condition, staticConditionObjects, conditionObjects);

                foreach (var node in nodes)
                {
                    if (node.Selector != null)
                    {
                        var si = selectorObjects.IndexOf(node.Selector);
                        if (si < 0) { selectorObjects.Add(node.Selector); }
                    }
                    CollectCondition(node.SelectorCallerCondition);
                    foreach (var caller in node.SelectorCallers) FindOrAdd(caller, staticSelectorCallerObjects, selectorCallerObjects);
                    foreach (var action in node.Actions) FindOrAdd(action.Instance, staticActionObjects, actionObjects);
                    foreach (var child in node.Children) CollectCondition(child.Condition);
                    foreach (var state in node.States)
                    {
                        CollectCondition(state.Condition);
                        foreach (var e in state.Events) FindOrAdd(e, staticTransitionEventObjects, transitionEventObjects);
                    }
                    foreach (var transition in node.Transitions) CollectCondition(transition.Condition);
                    foreach (var allState in node.AllStates) CollectCondition(allState.Condition);
                }

                RszFile BuildRsz(List<RszObjectNode> objects)
                {
                    var builder = new RszFile.Builder(Repository, RszVersion) { Objects = [.. objects] };
                    return builder.Build();
                }

                var actionRsz = BuildRsz(actionObjects);
                var staticActionRsz = BuildRsz(staticActionObjects);
                var selectorRsz = BuildRsz(selectorObjects);
                var selectorCallerRsz = BuildRsz(selectorCallerObjects);
                var staticSelectorCallerRsz = BuildRsz(staticSelectorCallerObjects);
                var conditionsRsz = BuildRsz(conditionObjects);
                var staticConditionsRsz = BuildRsz(staticConditionObjects);
                var transitionEventRsz = BuildRsz(transitionEventObjects);
                var staticTransitionEventRsz = BuildRsz(staticTransitionEventObjects);
                var expressionTreeConditionsRsz = BuildRsz([.. ExpressionTreeConditions]);
                var staticExpressionTreeConditionsRsz = BuildRsz([.. StaticExpressionTreeConditions]);

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                var headerSize = 8 + (Version >= 42 ? 4 : 0) + 14 * 8 + (Version >= 34 ? 8 : 0) + 24;
                bw.WriteZeros(headerSize);

                var header = new Dictionary<string, long>();

                header["node"] = ms.Position;
                bw.Write(nodes.Count);
                var nameOffsetPatchPositions = new long[nodes.Count];
                var refTreeOffsetPatchPositions = new long[nodes.Count];
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    var parentId = parentOf[node]?.Id ?? BhvtNodeId.Unset;

                    bw.Write(node.Id.Id);
                    bw.Write(node.Id.ExId);
                    nameOffsetPatchPositions[i] = ms.Position;
                    bw.Write(0); // nameOffset placeholder
                    bw.Write(parentId.Id);
                    bw.Write(parentId.ExId);

                    bw.Write(node.Children.Length);
                    foreach (var c in node.Children) bw.Write(c.Node.Id.Id);
                    foreach (var c in node.Children) bw.Write(c.Node.Id.ExId);
                    foreach (var c in node.Children) bw.Write(ToRawIdOrUnset(CollectCondition(c.Condition)));

                    var selectorId = node.Selector != null ? selectorObjects.IndexOf(node.Selector) : -1;
                    bw.Write(selectorId);
                    bw.Write(node.SelectorCallers.Length);
                    foreach (var caller in node.SelectorCallers) bw.Write(ToRawId(FindOrAdd(caller, staticSelectorCallerObjects, selectorCallerObjects)));
                    bw.Write(ToRawIdOrUnset(CollectCondition(node.SelectorCallerCondition)));

                    bw.Write(node.Actions.Length);
                    foreach (var a in node.Actions) bw.Write(GetActionId(a.Instance));
                    foreach (var a in node.Actions) bw.Write(a.ActionEx);

                    bw.Write(node.Priority);
                    bw.Write((ushort)node.Attributes);

                    if (node.Attributes.HasFlag(BhvtNodeAttributes.IsFsmNode))
                    {
                        bw.Write((ushort)node.WorkFlags);
                        bw.Write(node.NameHash);
                        bw.Write(node.FullNameHash);
                        bw.Write(node.Tags.Length);
                        foreach (var t in node.Tags) bw.Write(t);
                        bw.Write(node.IsBranch);
                        bw.Write(node.IsEnd);
                    }
                    else
                    {
                        bw.Write((ushort)0);
                    }

                    bw.Write(node.States.Length);
                    foreach (var s in node.States)
                    {
                        bw.Write(s.Events.Length);
                        foreach (var e in s.Events) bw.Write(ToRawId(FindOrAdd(e, staticTransitionEventObjects, transitionEventObjects)));
                    }
                    foreach (var s in node.States) bw.Write(s.Target.Id);
                    foreach (var s in node.States) bw.Write(ToRawIdOrUnset(CollectCondition(s.Condition)));
                    foreach (var s in node.States) bw.Write(s.TransitionMapId);
                    foreach (var s in node.States) bw.Write(s.Target.ExId);
                    foreach (var s in node.States) bw.Write(s.StateEx);

                    bw.Write(node.Transitions.Length);
                    var hasEventList = RszVersion >= 16;
                    foreach (var t in node.Transitions)
                    {
                        if (hasEventList)
                        {
                            bw.Write(t.RawEvents.Length);
                            foreach (var e in t.RawEvents) bw.Write(e);
                        }
                    }
                    foreach (var t in node.Transitions) bw.Write(t.Start.Id);
                    foreach (var t in node.Transitions) bw.Write(ToRawIdOrUnset(CollectCondition(t.Condition)));
                    foreach (var t in node.Transitions) bw.Write(t.Start.ExId);

                    if (!node.Attributes.HasFlag(BhvtNodeAttributes.HasReferenceTree))
                    {
                        bw.Write(node.AllStates.Length);
                        foreach (var s in node.AllStates) bw.Write(s.Target.Id);
                        foreach (var s in node.AllStates) bw.Write(ToRawIdOrUnset(CollectCondition(s.Condition)));
                        foreach (var s in node.AllStates) bw.Write(s.TransitionMapId);
                        foreach (var s in node.AllStates) bw.Write(s.Target.ExId);
                        foreach (var s in node.AllStates) bw.Write(s.TransitionAttributes);
                    }

                    refTreeOffsetPatchPositions[i] = ms.Position;
                    bw.Write(0); // referenceTreePathOffset placeholder
                }

                // Action-object index table: write-time bookkeeping the reader never actually uses.
                bw.Write(actionObjects.Count);
                for (var i = 0; i < actionObjects.Count; i++) bw.Write(0);
                bw.Write(staticActionObjects.Count);
                for (var i = 0; i < staticActionObjects.Count; i++) bw.Write(0);

                void WriteRszStream(string name, RszFile rsz)
                {
                    bw.Align(16);
                    header[name] = ms.Position;
                    bw.Write(rsz.Data.Span);
                }

                WriteRszStream("action", actionRsz);
                WriteRszStream("selector", selectorRsz);
                WriteRszStream("selectorCaller", selectorCallerRsz);
                WriteRszStream("conditions", conditionsRsz);
                WriteRszStream("transitionEvent", transitionEventRsz);
                WriteRszStream("expressionTreeConditions", expressionTreeConditionsRsz);
                WriteRszStream("staticAction", staticActionRsz);
                WriteRszStream("staticSelectorCaller", staticSelectorCallerRsz);
                WriteRszStream("staticConditions", staticConditionsRsz);
                WriteRszStream("staticTransitionEvent", staticTransitionEventRsz);
                WriteRszStream("staticExpressionTreeConditions", staticExpressionTreeConditionsRsz);

                bw.Align(16);
                header["referencePrefabGameObjects"] = ms.Position;
                bw.Write(GameObjectReferences.Length);
                foreach (var g in GameObjectReferences)
                {
                    bw.Write(g.Guid.ToByteArray());
                    bw.Write(g.Values.Length);
                    foreach (var v in g.Values) bw.Write(v);
                }

                // Name pool: sequential null-terminated UTF-16 strings, 4-byte total-char-count prefix.
                bw.Align(16);
                header["string"] = ms.Position;
                bw.WriteZeros(4);
                var nameStart = ms.Position;
                var namePatches = new (long pos, int charOffset)[nodes.Count];
                for (var i = 0; i < nodes.Count; i++)
                {
                    namePatches[i] = (nameOffsetPatchPositions[i], (int)(ms.Position - nameStart) / 2);
                    bw.WriteUTF16(nodes[i].Name);
                }
                var nameCharCount = (int)(ms.Position - nameStart) / 2;
                var afterNames = ms.Position;
                foreach (var (pos, charOffset) in namePatches)
                {
                    ms.Position = pos;
                    bw.Write(charOffset);
                }
                ms.Position = header["string"];
                bw.Write(nameCharCount);
                ms.Position = afterNames;

                // Resource-dependency pool: node reference-tree paths first (so referenceTreePathOffset
                // stays a stable char offset even if resources are appended later), then resource paths
                // scanned from the embedded RSZ streams, then any extras. Same sequential-strings format
                // as the name pool, but with an extra 4-byte item-count prefix.
                header["resourcePaths"] = ms.Position;
                bw.WriteZeros(8);
                var resourceStart = ms.Position;
                var resourcePatches = new List<(long pos, int charOffset)>();
                var resourceCount = 0;
                for (var i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Attributes.HasFlag(BhvtNodeAttributes.HasReferenceTree) && nodes[i].ReferenceTree is { } refTree)
                    {
                        resourcePatches.Add((refTreeOffsetPatchPositions[i], (int)(ms.Position - resourceStart) / 2));
                        bw.WriteUTF16(refTree);
                        resourceCount++;
                    }
                }
                var seenResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allObjects = actionObjects.Concat(staticActionObjects).Concat(selectorObjects)
                    .Concat(selectorCallerObjects).Concat(staticSelectorCallerObjects)
                    .Concat(conditionObjects).Concat(staticConditionObjects)
                    .Concat(transitionEventObjects).Concat(staticTransitionEventObjects)
                    .Concat(ExpressionTreeConditions).Concat(StaticExpressionTreeConditions);
                foreach (var obj in allObjects)
                {
                    obj.Visit(n =>
                    {
                        if (n is RszResourceNode { IsEmpty: false } resourceNode && seenResources.Add(resourceNode.Value!))
                        {
                            bw.WriteUTF16(resourceNode.Value!);
                            resourceCount++;
                        }
                    });
                }
                foreach (var extra in ExtraResources)
                {
                    if (seenResources.Add(extra))
                    {
                        bw.WriteUTF16(extra);
                        resourceCount++;
                    }
                }
                var resourceCharCount = (int)(ms.Position - resourceStart) / 2;
                var afterResources = ms.Position;
                foreach (var (pos, charOffset) in resourcePatches)
                {
                    ms.Position = pos;
                    bw.Write(charOffset);
                }
                ms.Position = header["resourcePaths"];
                bw.Write(resourceCount);
                bw.Write(resourceCharCount);
                ms.Position = afterResources;

                if (Version >= 34)
                {
                    // Userdata-path pool: always empty for now (no real .fsmv2 file has been seen using
                    // it), but wired so it fails loudly rather than silently corrupting if that changes.
                    bw.Align(16);
                    header["userdataPaths"] = ms.Position;
                    foreach (var rsz in new[] { actionRsz, staticActionRsz, selectorRsz, selectorCallerRsz, staticSelectorCallerRsz, conditionsRsz, staticConditionsRsz, transitionEventRsz, staticTransitionEventRsz, expressionTreeConditionsRsz, staticExpressionTreeConditionsRsz })
                    {
                        if (rsz.Version >= 16 && rsz.UserDataInfoPaths.Length > 0)
                            throw new NotSupportedException("This BHVT file references userdata, which isn't supported by the write path yet.");
                    }
                    bw.Write(0);
                    bw.WriteZeros(4);
                }

                if (UvarBlob != null)
                {
                    bw.Align(16);
                    var uvarBase = ms.Position;
                    header["variable"] = uvarBase;
                    var delta = uvarBase - UvarBlobOriginalOffset;
                    bw.Write(RebaseUvarOffsets(UvarBlob, delta));
                    // baseVariableOffset (the sub-variable-tree table) isn't part of the uvar's own
                    // structure -- it's whatever originally followed it, captured as part of the same
                    // verbatim blob -- so it shifts by the exact same delta as everything else in it.
                    header["baseVariable"] = UvarBlobOriginalBaseVariableOffset + delta;
                }
                else
                {
                    bw.Align(16);
                    header["variable"] = ms.Position;
                    WriteEmptyUvar(bw);
                    header["baseVariable"] = ms.Position;
                    bw.Write(0); // 0 sub-variable trees
                    bw.WriteZeros(4);
                }

                ms.Position = 0;
                bw.Write(MAGIC);
                bw.Write(0u); // hash: not meaningfully interpreted anywhere read so far; left at 0
                if (Version >= 42) bw.WriteZeros(4);
                bw.Write(header["node"]);
                bw.Write(header["action"]);
                bw.Write(header["selector"]);
                bw.Write(header["selectorCaller"]);
                bw.Write(header["conditions"]);
                bw.Write(header["transitionEvent"]);
                bw.Write(header["expressionTreeConditions"]);
                bw.Write(header["staticAction"]);
                bw.Write(header["staticSelectorCaller"]);
                bw.Write(header["staticConditions"]);
                bw.Write(header["staticTransitionEvent"]);
                bw.Write(header["staticExpressionTreeConditions"]);
                bw.Write(header["string"]);
                bw.Write(header["resourcePaths"]);
                if (Version >= 34) bw.Write(header["userdataPaths"]);
                bw.Write(header["variable"]);
                bw.Write(header["baseVariable"]);
                bw.Write(header["referencePrefabGameObjects"]);

                return new BhvtFile(Version, ms.ToArray());
            }

            /// <summary>
            /// UVar stores its internal pointers (StringsOffset/DataOffset/EmbedsInfoOffset/HashInfoOffset,
            /// all at the same byte offsets in both the V2 and V3 header layouts) as absolute positions in
            /// the *outer* container when embedded, rather than relative to its own start. Shifts them all
            /// by the same delta the whole blob moved by; 0 is treated as "absent" and left alone. Only
            /// the top-level header fields are adjusted -- deeper pointers (the hash/guid sub-tables,
            /// embedded child uvars) aren't, since every real file seen has them empty, and doing so
            /// correctly would require walking the entire structure recursively.
            /// </summary>
            private static byte[] RebaseUvarOffsets(byte[] blob, long delta)
            {
                var result = (byte[])blob.Clone();
                var span = result.AsSpan();
                foreach (var fieldOffset in new[] { 8, 16, 24, 32 })
                {
                    var value = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(span.Slice(fieldOffset, 8));
                    if (value != 0)
                    {
                        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(span.Slice(fieldOffset, 8), value + delta);
                    }
                }
                return result;
            }

            /// <summary>Writes a minimal, valid, empty (0 variables, 0 embeds) UVar V2 header -- the version real .fsmv2 files embed.</summary>
            private static void WriteEmptyUvar(BinaryWriter bw)
            {
                bw.Write(2u); // version
                bw.Write(0x72617675u); // "uvar" magic
                bw.WriteZeros(8); // StringsOffset
                bw.WriteZeros(8); // DataOffset
                bw.WriteZeros(8); // EmbedsInfoOffset
                bw.WriteZeros(8); // HashInfoOffset
                bw.WriteZeros(8); // UnknownHeaderValue (V2 only)
                bw.WriteZeros(4); // UvarHash
                bw.WriteZeros(2); // VariableCount
                bw.WriteZeros(2); // EmbedCount
            }
        }

        private ImmutableArray<string> ReadStringPool(int poolOffset, bool hasCount)
        {
            var prefixSize = hasCount ? 8 : 4;
            var charLen = ReadInt32(poolOffset + (hasCount ? 4 : 0));
            var count = hasCount ? ReadInt32(poolOffset) : -1;

            var result = ImmutableArray.CreateBuilder<string>();
            var pos = poolOffset + prefixSize;
            var end = poolOffset + prefixSize + charLen * 2;
            while (pos < end && (count < 0 || result.Count < count))
            {
                var s = data.ReadWString(pos);
                result.Add(s);
                pos += (s.Length + 1) * 2;
            }
            return result.ToImmutable();
        }

        private static RszObjectNode? GetByIndex(ImmutableArray<RszObjectNode> list, int index) =>
            index >= 0 && index < list.Length ? list[index] : null;

        /// <summary>The action's own id field value -- what nodes actually reference it by, not table position.</summary>
        internal static uint GetActionId(RszObjectNode obj) =>
            obj.Children.Length > ActionIdFieldIndex && obj.Children[ActionIdFieldIndex] is RszValueNode v
                ? unchecked((uint)v.AsInt32())
                : 0;

        private Dictionary<ulong, ImmutableArray<BhvtAction>> ResolveActions(
            ImmutableArray<RawNode> rawNodes,
            ImmutableArray<RszObjectNode> actionObjects,
            ImmutableArray<RszObjectNode> staticActionObjects)
        {
            var byId = new Dictionary<ulong, RszObjectNode>();
            foreach (var obj in actionObjects.Concat(staticActionObjects))
            {
                var id = (ulong)GetActionId(obj);
                var exId = 0u;
                while (!byId.TryAdd(id | ((ulong)exId << 32), obj)) exId++;
            }
            var original = new Dictionary<ulong, RszObjectNode>(byId);

            var result = new Dictionary<ulong, ImmutableArray<BhvtAction>>();
            foreach (var raw in rawNodes)
            {
                if (raw.Actions.Length == 0) continue;

                var actions = ImmutableArray.CreateBuilder<BhvtAction>(raw.Actions.Length);
                foreach (var (rawAction, actionEx) in raw.Actions)
                {
                    if (!byId.Remove(rawAction, out var matched))
                    {
                        var ex = 1u;
                        while (!byId.Remove(rawAction | ((ulong)ex << 32), out matched))
                        {
                            ex++;
                            if (ex >= 50)
                            {
                                original.TryGetValue(rawAction, out matched);
                                break;
                            }
                        }
                    }
                    if (matched != null)
                    {
                        actions.Add(new BhvtAction(matched, actionEx));
                    }
                }
                result[raw.Id.Packed] = actions.ToImmutable();
            }
            return result;
        }

        private ImmutableArray<RawNode> ReadRawNodes(int rszVersion)
        {
            var reader = new BhvtReader(data, (int)Header.NodeOffset);
            var count = reader.ReadInt32();
            if (count <= 0) return [];

            var result = ImmutableArray.CreateBuilder<RawNode>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(ReadRawNode(ref reader, rszVersion));
            }

            var namePoolStart = (int)Header.StringOffset + 4;
            var resourcePoolStart = (int)Header.ResourcePathsOffset + 8;
            for (var i = 0; i < result.Count; i++)
            {
                var raw = result[i];
                var name = data.ReadWString(namePoolStart + raw.NameOffset * 2);
                var referenceTree = raw.Attributes.HasFlag(BhvtNodeAttributes.HasReferenceTree)
                    ? data.ReadWString(resourcePoolStart + raw.ReferenceTreePathOffset * 2)
                    : null;
                result[i] = raw.WithNameAndReferenceTree(name, referenceTree);
            }

            return result.ToImmutable();
        }

        private RawNode ReadRawNode(ref BhvtReader reader, int rszVersion)
        {
            var id = reader.ReadNodeId();
            var nameOffset = reader.ReadInt32();
            var parentId = reader.ReadNodeId();

            var childCount = reader.ReadInt32();
            var childIds = reader.ReadUInt32Column(childCount);
            var childExIds = reader.ReadUInt32Column(childCount);
            var childConditionIds = reader.ReadUInt32Column(childCount);
            var children = ImmutableArray.CreateBuilder<(BhvtNodeId, RawId)>(childCount);
            for (var i = 0; i < childCount; i++)
            {
                children.Add((new BhvtNodeId(childIds[i], childExIds[i]), RawId.FromRaw(childConditionIds[i])));
            }

            var selectorId = reader.ReadInt32();
            var callerCount = reader.ReadInt32();
            var selectorCallerIds = reader.ReadRawIdList(callerCount);
            var selectorCallerConditionId = reader.ReadRawId();

            var actionCount = reader.ReadInt32();
            var actionIds = reader.ReadUInt32Column(actionCount);
            var actionExs = reader.ReadUInt32Column(actionCount);
            var actions = ImmutableArray.CreateBuilder<(uint, uint)>(actionCount);
            for (var i = 0; i < actionCount; i++) actions.Add((actionIds[i], actionExs[i]));

            var priority = reader.ReadInt32();
            var attributes = (BhvtNodeAttributes)reader.ReadUInt16();

            var workFlags = BhvtWorkFlags.None;
            uint nameHash = 0, fullNameHash = 0;
            var tags = ImmutableArray<uint>.Empty;
            bool isBranch = false, isEnd = false;
            if (attributes.HasFlag(BhvtNodeAttributes.IsFsmNode))
            {
                workFlags = (BhvtWorkFlags)reader.ReadUInt16();
                nameHash = reader.ReadUInt32();
                fullNameHash = reader.ReadUInt32();
                tags = reader.ReadUInt32List(reader.ReadInt32());
                isBranch = reader.ReadBool();
                isEnd = reader.ReadBool();
            }
            else
            {
                reader.Skip(2);
            }

            var states = ReadRawStates(ref reader);
            var transitions = ReadRawTransitions(ref reader, rszVersion);

            var allStates = ImmutableArray<RawAllState>.Empty;
            if (!attributes.HasFlag(BhvtNodeAttributes.HasReferenceTree))
            {
                allStates = ReadRawAllStates(ref reader);
            }

            var referenceTreePathOffset = reader.ReadInt32();

            return new RawNode(
                id, parentId, "", nameOffset, attributes, priority, isBranch, isEnd, workFlags,
                nameHash, fullNameHash, tags, children.ToImmutable(), selectorId, selectorCallerIds,
                selectorCallerConditionId, actions.ToImmutable(), states, transitions, allStates,
                referenceTreePathOffset, null);
        }

        private static ImmutableArray<RawState> ReadRawStates(ref BhvtReader reader)
        {
            var count = reader.ReadInt32();
            if (count <= 0) return [];

            var eventLists = new ImmutableArray<RawId>[count];
            for (var i = 0; i < count; i++)
            {
                eventLists[i] = reader.ReadRawIdList(reader.ReadInt32());
            }

            var targetIds = reader.ReadUInt32Column(count);
            var conditionIds = reader.ReadUInt32Column(count);
            var transitionMapIds = reader.ReadUInt32Column(count);
            var targetExIds = reader.ReadUInt32Column(count);
            var stateExs = reader.ReadUInt32Column(count);

            var result = ImmutableArray.CreateBuilder<RawState>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(new RawState(
                    new BhvtNodeId(targetIds[i], targetExIds[i]),
                    RawId.FromRaw(conditionIds[i]),
                    transitionMapIds[i],
                    stateExs[i],
                    eventLists[i]));
            }
            return result.ToImmutable();
        }

        private static ImmutableArray<RawTransition> ReadRawTransitions(ref BhvtReader reader, int rszVersion)
        {
            var count = reader.ReadInt32();
            if (count <= 0) return [];

            var hasEventList = rszVersion >= 16;
            var eventLists = new ImmutableArray<uint>[count];
            for (var i = 0; i < count; i++)
            {
                eventLists[i] = hasEventList ? reader.ReadUInt32List(reader.ReadInt32()) : [];
            }

            var startIds = reader.ReadUInt32Column(count);
            var conditionIds = reader.ReadUInt32Column(count);
            var startExIds = reader.ReadUInt32Column(count);

            var result = ImmutableArray.CreateBuilder<RawTransition>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(new RawTransition(
                    new BhvtNodeId(startIds[i], startExIds[i]),
                    RawId.FromRaw(conditionIds[i]),
                    eventLists[i]));
            }
            return result.ToImmutable();
        }

        private static ImmutableArray<RawAllState> ReadRawAllStates(ref BhvtReader reader)
        {
            var count = reader.ReadInt32();
            if (count <= 0) return [];

            var targetIds = reader.ReadUInt32Column(count);
            var conditionIds = reader.ReadUInt32Column(count);
            var transitionMapIds = reader.ReadUInt32Column(count);
            var targetExIds = reader.ReadUInt32Column(count);
            var transitionAttributes = reader.ReadUInt32Column(count);

            var result = ImmutableArray.CreateBuilder<RawAllState>(count);
            for (var i = 0; i < count; i++)
            {
                result.Add(new RawAllState(
                    new BhvtNodeId(targetIds[i], targetExIds[i]),
                    RawId.FromRaw(conditionIds[i]),
                    transitionMapIds[i],
                    transitionAttributes[i]));
            }
            return result.ToImmutable();
        }

        private uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Span.Slice(offset, 4));
        private int ReadInt32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.Span.Slice(offset, 4));
        private long ReadInt64(int offset) => BinaryPrimitives.ReadInt64LittleEndian(data.Span.Slice(offset, 8));

        /// <summary>
        /// A packed reference into one of the paired (non-static / static) RSZ object tables: low 16 bits
        /// are the object table index, next byte is unused/unknown, top byte's value 64 (0x40) selects
        /// the "static" table instead of the regular one. Purely a binary-layout detail -- once resolved
        /// during <see cref="ReadTree"/>, callers only ever see the actual <see cref="RszObjectNode"/>.
        /// </summary>
        private readonly struct RawId(ushort index, byte unknown, byte idType)
        {
            public ushort Index { get; } = index;
            public byte Unknown { get; } = unknown;
            public byte IdType { get; } = idType;
            public bool IsStatic => IdType == 64;
            public bool HasValue => Index != ushort.MaxValue;

            public static RawId FromRaw(uint raw) => new(
                (ushort)(raw & 0xFFFF),
                (byte)((raw >> 16) & 0xFF),
                (byte)((raw >> 24) & 0xFF));
        }

        private sealed record RawNode(
            BhvtNodeId Id,
            BhvtNodeId ParentId,
            string Name,
            int NameOffset,
            BhvtNodeAttributes Attributes,
            int Priority,
            bool IsBranch,
            bool IsEnd,
            BhvtWorkFlags WorkFlags,
            uint NameHash,
            uint FullNameHash,
            ImmutableArray<uint> Tags,
            ImmutableArray<(BhvtNodeId ChildId, RawId ConditionId)> Children,
            int SelectorId,
            ImmutableArray<RawId> SelectorCallerIds,
            RawId SelectorCallerConditionId,
            ImmutableArray<(uint Action, uint ActionEx)> Actions,
            ImmutableArray<RawState> States,
            ImmutableArray<RawTransition> Transitions,
            ImmutableArray<RawAllState> AllStates,
            int ReferenceTreePathOffset,
            string? ReferenceTree)
        {
            public RawNode WithNameAndReferenceTree(string name, string? referenceTree) =>
                this with { Name = name, ReferenceTree = referenceTree };
        }

        private sealed record RawState(BhvtNodeId Target, RawId ConditionId, uint TransitionMapId, uint StateEx, ImmutableArray<RawId> EventIds);
        private sealed record RawTransition(BhvtNodeId Start, RawId ConditionId, ImmutableArray<uint> RawEvents);
        private sealed record RawAllState(BhvtNodeId Target, RawId ConditionId, uint TransitionMapId, uint TransitionAttributes);

        private ref struct BhvtReader(ReadOnlyMemory<byte> data, int position)
        {
            private readonly ReadOnlySpan<byte> _span = data.Span;
            private int _position = position;

            public readonly int Position => _position;

            public int ReadInt32()
            {
                var v = BinaryPrimitives.ReadInt32LittleEndian(_span.Slice(_position, 4));
                _position += 4;
                return v;
            }

            public uint ReadUInt32()
            {
                var v = BinaryPrimitives.ReadUInt32LittleEndian(_span.Slice(_position, 4));
                _position += 4;
                return v;
            }

            public ushort ReadUInt16()
            {
                var v = BinaryPrimitives.ReadUInt16LittleEndian(_span.Slice(_position, 2));
                _position += 2;
                return v;
            }

            public byte ReadByte() => _span[_position++];
            public bool ReadBool() => ReadByte() != 0;
            public void Skip(int count) => _position += count;

            public Guid ReadGuid()
            {
                var v = new Guid(_span.Slice(_position, 16));
                _position += 16;
                return v;
            }

            public BhvtNodeId ReadNodeId()
            {
                var id = ReadUInt32();
                var exId = ReadUInt32();
                return new BhvtNodeId(id, exId);
            }

            public RawId ReadRawId() => RawId.FromRaw(ReadUInt32());

            public ImmutableArray<RawId> ReadRawIdList(int count)
            {
                if (count <= 0) return [];
                var result = ImmutableArray.CreateBuilder<RawId>(count);
                for (var i = 0; i < count; i++) result.Add(ReadRawId());
                return result.ToImmutable();
            }

            public ImmutableArray<uint> ReadUInt32List(int count)
            {
                if (count <= 0) return [];
                var result = ImmutableArray.CreateBuilder<uint>(count);
                for (var i = 0; i < count; i++) result.Add(ReadUInt32());
                return result.ToImmutable();
            }

            public ImmutableArray<int> ReadInt32List(int count)
            {
                if (count <= 0) return [];
                var result = ImmutableArray.CreateBuilder<int>(count);
                for (var i = 0; i < count; i++) result.Add(ReadInt32());
                return result.ToImmutable();
            }

            /// <summary>
            /// Reads one column of a "struct of arrays" table: a fixed number of uint columns are stored
            /// back-to-back, each column holding `count` consecutive values for one field across all rows.
            /// </summary>
            public uint[] ReadUInt32Column(int count)
            {
                var result = new uint[count];
                for (var i = 0; i < count; i++) result[i] = ReadUInt32();
                return result;
            }
        }
    }
}
