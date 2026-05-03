using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard.REE.Fsm
{
    /// <summary>
    /// Lossless graph-oriented representation of an HFSM file. Parsed state and transition records are exposed
    /// for inspection/editing, while opaque RSZ/UVAR data and unmodeled section tails are preserved for rebuilds.
    /// </summary>
    public sealed class HfsmGraphDocument
    {
        private const uint MAGIC = 0x4D534648;
        private const int FixedHeaderSize = 0x64;

        public int FormatVersion { get; set; } = 1;
        public int Version { get; set; }
        public uint HeaderFlags { get; set; }
        public int HeaderSize { get; set; } = FixedHeaderSize;
        public byte[] HeaderTail { get; set; } = Array.Empty<byte>();
        public List<HfsmGraphString> Strings { get; set; } = new();
        public List<HfsmGraphString> ExtraStrings { get; set; } = new();
        public List<HfsmGraphState> States { get; set; } = new();
        public List<HfsmGraphTransitionGroup> TransitionGroups { get; set; } = new();
        public List<HfsmGraphTransitionInfo> TransitionInfos { get; set; } = new();
        public List<HfsmGraphActionReference> ActionReferences { get; set; } = new();
        public HfsmGraphRawSections RawSections { get; set; } = new();

        public static HfsmGraphDocument FromFile(HfsmFile file)
        {
            var result = new HfsmGraphDocument
            {
                Version = file.Version,
                HeaderFlags = file.HeaderFlags,
                HeaderSize = file.HeaderSize,
                HeaderTail = file.HeaderSize > FixedHeaderSize
                    ? file.Data.Slice(FixedHeaderSize, file.HeaderSize - FixedHeaderSize).ToArray()
                    : Array.Empty<byte>(),
                Strings = [.. file.Strings.Select(x => new HfsmGraphString
                {
                    CharOffset = x.CharOffset,
                    Value = x.Value
                })],
                ExtraStrings = [.. file.ExtraStrings.Select(x => new HfsmGraphString
                {
                    CharOffset = x.CharOffset,
                    Value = x.Value
                })],
                States = [.. file.StateEntries.Select(x => new HfsmGraphState
                {
                    Index = x.Index,
                    ParentIndex = x.ParentIndex,
                    Depth = x.Depth,
                    StateId = x.StateId,
                    Unknown4 = x.Unknown4,
                    TagIds = [.. x.TagIds],
                    ActionReferences = [.. x.ActionReferences.Select(y => new HfsmGraphStateActionReference
                    {
                        Uid = y.Uid,
                        ListNo = y.ListNo
                    })],
                    TransitionGroupIndex = x.TransitionGroupIndex,
                    InitialStateId = x.InitialStateId,
                    StateDataObjects = [.. x.StateDataObjects.Select(y => new HfsmGraphStateDataObject
                    {
                        StateId = y.StateId,
                        StateUnknown = y.StateUnknown,
                        TransitionId = y.TransitionId,
                        TransitionUnknown = y.TransitionUnknown,
                        Unknown0 = y.Unknown0,
                        Unknown1 = y.Unknown1
                    })],
                    NameCharOffset = x.NameCharOffset,
                    Name = x.Name,
                    ChildIndices = [.. x.ChildIndices]
                })],
                TransitionGroups = [.. file.TransitionGroups.Select(x => new HfsmGraphTransitionGroup
                {
                    Type = x.Type,
                    Unknown0 = x.Unknown0,
                    DeclaredStateCount = x.DeclaredStateCount,
                    StateIds = [.. x.StateIds.Select(y => new HfsmGraphStateId
                    {
                        Id = y.Id,
                        Unknown = y.Unknown
                    })],
                    Transitions = [.. x.Transitions.Select(y => new HfsmGraphTransition
                    {
                        FromStateId = y.FromStateId,
                        FromStateUnknown = y.FromStateUnknown,
                        ToStateId = y.ToStateId,
                        ToStateUnknown = y.ToStateUnknown,
                        TransitionId = y.TransitionId,
                        TransitionUnknown = y.TransitionUnknown,
                        Unknown0 = y.Unknown0,
                        Unknown1 = y.Unknown1
                    })]
                })],
                TransitionInfos = [.. file.TransitionInfos.Select(x => new HfsmGraphTransitionInfo
                {
                    TransitionId = x.TransitionId,
                    ConditionNameCharOffset = x.ConditionNameCharOffset,
                    ConditionName = x.ConditionName,
                    ConditionObjectIndex = x.ConditionObjectIndex,
                    Expression = x.Expression,
                    Enabled = x.Enabled,
                    Condition = x.Condition,
                    ExpressionReferenceType = x.ExpressionReferenceType,
                    Padding = x.Padding
                })],
                ActionReferences = [.. file.ActionReferences.Select(x => new HfsmGraphActionReference
                {
                    Uid = x.Uid,
                    ListNo = x.ListNo,
                    ObjectIndex = x.ObjectIndex
                })],
                RawSections = new HfsmGraphRawSections
                {
                    ActionData = file.GetSection(HfsmSectionKind.ActionData).Data.ToArray(),
                    ConditionData = file.GetSection(HfsmSectionKind.ConditionData).Data.ToArray(),
                    SelectorData = file.GetSection(HfsmSectionKind.SelectorData).Data.ToArray(),
                    ExpressionData = file.GetSection(HfsmSectionKind.ExpressionData).Data.ToArray(),
                    StringTable = file.GetSection(HfsmSectionKind.StringTable).Data.ToArray(),
                    ExtraStringTable = file.GetSection(HfsmSectionKind.ExtraStringTable).Data.ToArray(),
                    UserVariables = file.GetSection(HfsmSectionKind.UserVariables).Data.ToArray()
                }
            };
            result.RawSections.StateDataTail = result.GetSectionTail(
                HfsmSectionKind.StateData,
                file.GetSection(HfsmSectionKind.StateData).Data.ToArray(),
                result.WriteStateDataCore);
            result.RawSections.TransitionGraphTail = result.GetSectionTail(
                HfsmSectionKind.TransitionGraph,
                file.GetSection(HfsmSectionKind.TransitionGraph).Data.ToArray(),
                result.WriteTransitionGraphCore);
            result.RawSections.TransitionInfoTail = result.GetSectionTail(
                HfsmSectionKind.TransitionInfo,
                file.GetSection(HfsmSectionKind.TransitionInfo).Data.ToArray(),
                result.WriteTransitionInfoCore);
            result.RawSections.ActionReferenceTableTail = result.GetSectionTail(
                HfsmSectionKind.ActionReferenceTable,
                file.GetSection(HfsmSectionKind.ActionReferenceTable).Data.ToArray(),
                result.WriteActionReferencesCore);
            return result;
        }

        public HfsmFile Build()
        {
            if (FormatVersion != 1)
                throw new InvalidDataException($"Unsupported HFSM graph format version: {FormatVersion}.");
            if (HeaderSize < FixedHeaderSize)
                throw new InvalidDataException($"HFSM graph header size is too small: 0x{HeaderSize:X}.");
            if (HeaderTail.Length != HeaderSize - FixedHeaderSize)
                throw new InvalidDataException("HFSM graph header tail length does not match the header size.");

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(MAGIC);
            bw.Write(Version);
            bw.Write(HeaderFlags);
            for (var i = 0; i < 11; i++)
            {
                bw.Write(0L);
            }
            bw.Write(HeaderTail);

            var stateDataOffset = ms.Position;
            WriteStateData(bw);
            var actionDataOffset = ms.Position;
            bw.Write(RawSections.ActionData);
            var transitionGraphOffset = ms.Position;
            WriteTransitionGraph(bw);
            var transitionInfoOffset = ms.Position;
            WriteTransitionInfo(bw);
            var conditionDataOffset = ms.Position;
            bw.Write(RawSections.ConditionData);
            var selectorDataOffset = ms.Position;
            bw.Write(RawSections.SelectorData);
            var expressionDataOffset = ms.Position;
            bw.Write(RawSections.ExpressionData);
            var stringTableOffset = ms.Position;
            bw.Write(RawSections.StringTable);
            var extraStringTableOffset = ms.Position;
            bw.Write(RawSections.ExtraStringTable);
            var actionReferenceTableOffset = ms.Position;
            WriteActionReferences(bw);
            var userVariablesOffset = ms.Position;
            bw.Write(RawSections.UserVariables);

            ms.Position = 0x0C;
            bw.Write(stateDataOffset);
            bw.Write(actionDataOffset);
            bw.Write(transitionGraphOffset);
            bw.Write(transitionInfoOffset);
            bw.Write(conditionDataOffset);
            bw.Write(selectorDataOffset);
            bw.Write(expressionDataOffset);
            bw.Write(userVariablesOffset);
            bw.Write(stringTableOffset);
            bw.Write(extraStringTableOffset);
            bw.Write(actionReferenceTableOffset);

            return new HfsmFile(ms.ToArray());
        }

        public string ToDot()
        {
            var sb = new StringBuilder();
            var statesById = States
                .Where(x => x.StateId != 0)
                .GroupBy(x => x.StateId)
                .ToDictionary(x => x.Key, x => x.First().Index);
            var transitionInfoById = TransitionInfos
                .GroupBy(x => x.TransitionId)
                .ToDictionary(x => x.Key, x => x.First());

            sb.AppendLine("digraph HFSM {");
            sb.AppendLine("  graph [rankdir=LR];");
            sb.AppendLine("  node [shape=box, fontname=\"Consolas\"];");
            sb.AppendLine("  edge [fontname=\"Consolas\"];");
            sb.AppendLine();

            foreach (var state in States.OrderBy(x => x.Index))
            {
                sb.Append("  ");
                sb.Append(GetNodeId(state.Index));
                sb.Append(" [label=\"");
                sb.Append(EscapeDotLabel(GetStateLabel(state)));
                sb.AppendLine("\"];");
            }

            sb.AppendLine();
            foreach (var state in States.OrderBy(x => x.Index))
            {
                foreach (var childIndex in GetChildIndices(state).OrderBy(x => x))
                {
                    sb.Append("  ");
                    sb.Append(GetNodeId(state.Index));
                    sb.Append(" -> ");
                    sb.Append(GetNodeId(childIndex));
                    sb.AppendLine(" [label=\"child\", color=\"#999999\", style=dotted];");
                }
            }

            foreach (var group in TransitionGroups)
            {
                foreach (var transition in group.Transitions)
                {
                    sb.Append("  ");
                    sb.Append(GetTransitionNodeId(transition.FromStateId, statesById));
                    sb.Append(" -> ");
                    sb.Append(GetTransitionNodeId(transition.ToStateId, statesById));
                    sb.Append(" [label=\"");
                    sb.Append(EscapeDotLabel(GetTransitionLabel(transition, transitionInfoById)));
                    sb.AppendLine("\", color=\"#005f99\"];");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private void WriteStateData(BinaryWriter bw)
        {
            WriteStateDataCore(bw);
            bw.Write(RawSections.StateDataTail);
        }

        private void WriteStateDataCore(BinaryWriter bw)
        {
            bw.Write(States.Count);
            if (States.Count == 0)
                return;

            var stateByIndex = States.ToDictionary(x => x.Index);
            var visited = new HashSet<int>();
            var rootIndices = States
                .Where(x => x.ParentIndex < 0)
                .OrderBy(x => x.Index)
                .Select(x => x.Index)
                .ToArray();
            if (rootIndices.Length == 0)
                throw new InvalidDataException("HFSM graph contains states but no root state.");

            foreach (var rootIndex in rootIndices)
            {
                WriteStateEntry(bw, stateByIndex, rootIndex, visited);
            }
            if (visited.Count != States.Count)
                throw new InvalidDataException("HFSM graph state hierarchy does not contain every state.");
        }

        private void WriteStateEntry(
            BinaryWriter bw,
            Dictionary<int, HfsmGraphState> stateByIndex,
            int index,
            HashSet<int> visited)
        {
            if (!stateByIndex.TryGetValue(index, out var state))
                throw new InvalidDataException($"HFSM graph references missing state index {index}.");
            if (!visited.Add(index))
                throw new InvalidDataException($"HFSM graph contains a cycle or duplicate state index {index}.");

            bw.Write(state.StateId);
            bw.Write(state.Unknown4);
            bw.Write(state.TagIds.Count);
            foreach (var tagId in state.TagIds)
            {
                bw.Write(tagId);
            }
            bw.Write(state.ActionReferences.Count);
            foreach (var actionReference in state.ActionReferences)
            {
                bw.Write(actionReference.Uid);
                bw.Write(actionReference.ListNo);
            }
            bw.Write(state.TransitionGroupIndex);
            bw.Write(state.InitialStateId);
            bw.Write(state.StateDataObjects.Count);
            foreach (var stateDataObject in state.StateDataObjects)
            {
                bw.Write(stateDataObject.StateId);
                bw.Write(stateDataObject.StateUnknown);
                bw.Write(stateDataObject.TransitionId);
                bw.Write(stateDataObject.TransitionUnknown);
                bw.Write(stateDataObject.Unknown0);
                bw.Write(stateDataObject.Unknown1);
            }
            bw.Write(state.NameCharOffset);
            var childIndices = GetChildIndices(state).ToArray();
            bw.Write(childIndices.Length);
            foreach (var childIndex in childIndices)
            {
                WriteStateEntry(bw, stateByIndex, childIndex, visited);
            }
        }

        private void WriteTransitionGraph(BinaryWriter bw)
        {
            WriteTransitionGraphCore(bw);
            bw.Write(RawSections.TransitionGraphTail);
        }

        private void WriteTransitionGraphCore(BinaryWriter bw)
        {
            bw.Write(TransitionGroups.Count);
            foreach (var group in TransitionGroups)
            {
                bw.Write(group.Type);
                bw.Write(group.Unknown0);
                bw.Write(group.DeclaredStateCount);
                bw.Write(group.StateIds.Count);
                foreach (var stateId in group.StateIds)
                {
                    bw.Write(stateId.Id);
                    bw.Write(stateId.Unknown);
                }
                bw.Write(group.Transitions.Count);
                foreach (var transition in group.Transitions)
                {
                    bw.Write(transition.FromStateId);
                    bw.Write(transition.FromStateUnknown);
                    bw.Write(transition.ToStateId);
                    bw.Write(transition.ToStateUnknown);
                    bw.Write(transition.TransitionId);
                    bw.Write(transition.TransitionUnknown);
                    bw.Write(transition.Unknown0);
                    bw.Write(transition.Unknown1);
                }
            }
        }

        private void WriteTransitionInfo(BinaryWriter bw)
        {
            WriteTransitionInfoCore(bw);
            bw.Write(RawSections.TransitionInfoTail);
        }

        private void WriteTransitionInfoCore(BinaryWriter bw)
        {
            bw.Write(TransitionInfos.Count);
            foreach (var info in TransitionInfos)
            {
                bw.Write(info.TransitionId);
                bw.Write(info.ConditionNameCharOffset);
                bw.Write(info.ConditionObjectIndex);
                bw.Write(info.Expression.ToByteArray());
                bw.Write((byte)(info.Enabled ? 1 : 0));
                bw.Write((byte)(info.Condition ? 1 : 0));
                bw.Write((byte)info.ExpressionReferenceType);
                bw.Write(info.Padding);
            }
        }

        private void WriteActionReferences(BinaryWriter bw)
        {
            WriteActionReferencesCore(bw);
            bw.Write(RawSections.ActionReferenceTableTail);
        }

        private void WriteActionReferencesCore(BinaryWriter bw)
        {
            bw.Write(ActionReferences.Count);
            foreach (var actionReference in ActionReferences)
            {
                bw.Write(actionReference.Uid);
                bw.Write(actionReference.ListNo);
                bw.Write(actionReference.ObjectIndex);
            }
        }

        private byte[] GetSectionTail(HfsmSectionKind kind, byte[] original, Action<BinaryWriter> writeCore)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            writeCore(bw);
            var core = ms.ToArray();
            if (core.Length > original.Length || !original.AsSpan(0, core.Length).SequenceEqual(core))
            {
                throw new InvalidDataException($"HFSM graph section {kind} contains data that is not modeled.");
            }
            return original.AsSpan(core.Length).ToArray();
        }

        private IEnumerable<int> GetChildIndices(HfsmGraphState state)
        {
            return state.ChildIndices.Count != 0
                ? state.ChildIndices
                : States.Where(x => x.ParentIndex == state.Index).OrderBy(x => x.Index).Select(x => x.Index);
        }

        private static string GetNodeId(int index) => $"state_{index}";

        private static string GetTransitionNodeId(uint stateId, Dictionary<uint, int> statesById)
        {
            return statesById.TryGetValue(stateId, out var index)
                ? GetNodeId(index)
                : $"missing_{stateId:X8}";
        }

        private static string GetStateLabel(HfsmGraphState state)
        {
            var name = string.IsNullOrEmpty(state.Name) ? "<unnamed>" : state.Name;
            return $"{name}\\nstate 0x{state.StateId:X8}\\nindex {state.Index}";
        }

        private static string GetTransitionLabel(
            HfsmGraphTransition transition,
            Dictionary<uint, HfsmGraphTransitionInfo> transitionInfoById)
        {
            if (transitionInfoById.TryGetValue(transition.TransitionId, out var info))
            {
                var conditionName = string.IsNullOrEmpty(info.ConditionName) ? "<default>" : info.ConditionName;
                return $"0x{transition.TransitionId:X8}\\n{conditionName}";
            }
            return $"0x{transition.TransitionId:X8}";
        }

        private static string EscapeDotLabel(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "")
                .Replace("\n", "\\n");
        }
    }

    public sealed class HfsmGraphRawSections
    {
        public byte[] StateDataTail { get; set; } = Array.Empty<byte>();
        public byte[] ActionData { get; set; } = Array.Empty<byte>();
        public byte[] TransitionGraphTail { get; set; } = Array.Empty<byte>();
        public byte[] TransitionInfoTail { get; set; } = Array.Empty<byte>();
        public byte[] ConditionData { get; set; } = Array.Empty<byte>();
        public byte[] SelectorData { get; set; } = Array.Empty<byte>();
        public byte[] ExpressionData { get; set; } = Array.Empty<byte>();
        public byte[] StringTable { get; set; } = Array.Empty<byte>();
        public byte[] ExtraStringTable { get; set; } = Array.Empty<byte>();
        public byte[] ActionReferenceTableTail { get; set; } = Array.Empty<byte>();
        public byte[] UserVariables { get; set; } = Array.Empty<byte>();
    }

    public sealed class HfsmGraphString
    {
        public int CharOffset { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public sealed class HfsmGraphState
    {
        public int Index { get; set; }
        public int ParentIndex { get; set; }
        public int Depth { get; set; }
        public uint StateId { get; set; }
        public int Unknown4 { get; set; }
        public List<uint> TagIds { get; set; } = new();
        public List<HfsmGraphStateActionReference> ActionReferences { get; set; } = new();
        public int TransitionGroupIndex { get; set; }
        public uint InitialStateId { get; set; }
        public List<HfsmGraphStateDataObject> StateDataObjects { get; set; } = new();
        public int NameCharOffset { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<int> ChildIndices { get; set; } = new();
    }

    public sealed class HfsmGraphStateActionReference
    {
        public uint Uid { get; set; }
        public byte ListNo { get; set; }
    }

    public sealed class HfsmGraphStateDataObject
    {
        public uint StateId { get; set; }
        public uint StateUnknown { get; set; }
        public uint TransitionId { get; set; }
        public uint TransitionUnknown { get; set; }
        public uint Unknown0 { get; set; }
        public uint Unknown1 { get; set; }
    }

    public sealed class HfsmGraphStateId
    {
        public uint Id { get; set; }
        public uint Unknown { get; set; }
    }

    public sealed class HfsmGraphTransitionGroup
    {
        public uint Type { get; set; }
        public uint Unknown0 { get; set; }
        public int DeclaredStateCount { get; set; }
        public List<HfsmGraphStateId> StateIds { get; set; } = new();
        public List<HfsmGraphTransition> Transitions { get; set; } = new();
    }

    public sealed class HfsmGraphTransition
    {
        public uint FromStateId { get; set; }
        public uint FromStateUnknown { get; set; }
        public uint ToStateId { get; set; }
        public uint ToStateUnknown { get; set; }
        public uint TransitionId { get; set; }
        public uint TransitionUnknown { get; set; }
        public uint Unknown0 { get; set; }
        public uint Unknown1 { get; set; }
    }

    public sealed class HfsmGraphTransitionInfo
    {
        public uint TransitionId { get; set; }
        public int ConditionNameCharOffset { get; set; }
        public string ConditionName { get; set; } = string.Empty;
        public int ConditionObjectIndex { get; set; }
        public Guid Expression { get; set; }
        public bool Enabled { get; set; }
        public bool Condition { get; set; }
        public HfsmExpressionReferenceType ExpressionReferenceType { get; set; }
        public byte Padding { get; set; }
    }

    public sealed class HfsmGraphActionReference
    {
        public uint Uid { get; set; }
        public uint ListNo { get; set; }
        public int ObjectIndex { get; set; }
    }
}
