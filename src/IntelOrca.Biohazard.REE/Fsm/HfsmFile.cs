using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using IntelOrca.Biohazard.REE.Rsz;
using IntelOrca.Biohazard.REE.Variables;

namespace IntelOrca.Biohazard.REE.Fsm
{
    /// <summary>
    /// Hierarchical finite state machine (HFSM) files.
    /// Version 16 is used by Resident Evil 7.
    /// </summary>
    public sealed class HfsmFile
    {
        private const uint MAGIC = 0x4D534648;
        private const int HeaderSizeOffset = 0x0C;
        private readonly ImmutableArray<HfsmStringTableEntry> _strings;
        private readonly ImmutableArray<HfsmStringTableEntry> _extraStrings;
        private readonly ImmutableArray<HfsmStateEntry> _stateEntries;
        private readonly ImmutableArray<HfsmTransitionGroup> _transitionGroups;
        private readonly ImmutableArray<HfsmTransitionInfo> _transitionInfos;
        private readonly ImmutableArray<HfsmActionReference> _actionReferences;
        private readonly ImmutableArray<HfsmState> _states;

        public HfsmFile(ReadOnlyMemory<byte> data)
        {
            Data = data;
            if (data.Length < HeaderSizeOffset + 8)
                throw new InvalidDataException("HFSM data is smaller than the fixed header prefix.");
            if (Magic != MAGIC)
                throw new InvalidDataException("HFSM magic is invalid.");
            if (data.Length < HeaderSize)
                throw new InvalidDataException("HFSM data is smaller than the declared header size.");

            _strings = ReadStringTable();
            _extraStrings = ReadExtraStringTable();
            _stateEntries = ReadStateEntries();
            _transitionGroups = ReadTransitionGroups();
            _transitionInfos = ReadTransitionInfos();
            _actionReferences = ReadActionReferences();
            _states = BuildStates();
        }

        public ReadOnlyMemory<byte> Data { get; }

        public uint Magic => ReadUInt32(0);
        public int Version => ReadInt32(4);

        /// <summary>
        /// Single-byte header flag at 0x08.
        /// TODO: Determine the meaning of the flag bits and expose them as properties.
        /// </summary>
        public byte Flags => Data.Span[8];

        /// <summary>
        /// Raw 32-bit view of the flags/padding dword.
        /// </summary>
        public uint HeaderFlags => ReadUInt32(8);

        public int HeaderSize => ReadOffset(HeaderSizeOffset);
        public int StateDataOffset => HeaderSize;
        public int ActionDataOffset => ReadOffset(0x14);
        public int TransitionGraphOffset => ReadOffset(0x1C);
        public int TransitionInfoOffset => ReadOffset(0x24);
        public int ConditionDataOffset => ReadOffset(0x2C);
        public int SelectorDataOffset => ReadOffset(0x34);
        public int ExpressionDataOffset => ReadOffset(0x3C);
        public int UserVariablesOffset => ReadOffset(0x44);
        public int StringTableOffset => ReadOffset(0x4C);
        public int ExtraStringTableOffset => ReadOffset(0x54);
        public int ActionReferenceTableOffset => ReadOffset(0x5C);

        public int UnknownTableOffset => ExtraStringTableOffset;
        public int ReferenceTableOffset => ActionReferenceTableOffset;

        /// <summary>
        /// Number of state records in the recursive state tree stored at the start of the state section.
        /// </summary>
        public int StateDataEntryCount => ReadInt32(StateDataOffset);

        public ImmutableArray<HfsmStringTableEntry> Strings => _strings;

        /// <summary>
        /// Optional counted UTF-16 string table after the main string table.
        /// </summary>
        public ImmutableArray<HfsmStringTableEntry> ExtraStrings => _extraStrings;

        /// <summary>
        /// Recursive state tree records read from the state data section.
        /// </summary>
        public ImmutableArray<HfsmStateEntry> StateEntries => _stateEntries;

        /// <summary>
        /// State IDs and names read from the state data section. The root state with ID 0 is omitted.
        /// </summary>
        public ImmutableArray<HfsmState> States => _states;

        /// <summary>
        /// Parsed transition graph groups.
        /// </summary>
        public ImmutableArray<HfsmTransitionGroup> TransitionGroups => _transitionGroups;

        /// <summary>
        /// Transition condition records. These set the condition UID to the transition ID and contain the condition
        /// type/name string offset plus base via.fsm.Condition fields.
        /// </summary>
        public ImmutableArray<HfsmTransitionInfo> TransitionInfos => _transitionInfos;

        /// <summary>
        /// Action UID/ListNo to action object index map used when state action references are resolved.
        /// For example, TempFsm_TriggerInAction_EnemyGenerate uses action UID 0xAA801BF0 with ListNo 0.
        /// </summary>
        public ImmutableArray<HfsmActionReference> ActionReferences => _actionReferences;

        public ImmutableArray<HfsmActionReference> References => _actionReferences;

        public RszFile ActionData => ReadRsz(HfsmSectionKind.ActionData);
        public RszFile ConditionData => ReadRsz(HfsmSectionKind.ConditionData);
        public RszFile SelectorData => ReadRsz(HfsmSectionKind.SelectorData);
        public RszFile ExpressionData => ReadRsz(HfsmSectionKind.ExpressionData);
        public UvarFile UserVariables => new(GetSection(HfsmSectionKind.UserVariables).Data);

        public ImmutableArray<HfsmRawSection> Sections
        {
            get
            {
                var builder = ImmutableArray.CreateBuilder<HfsmRawSection>();
                foreach (var kind in Enum.GetValues(typeof(HfsmSectionKind)).Cast<HfsmSectionKind>())
                {
                    builder.Add(GetSection(kind));
                }
                return builder.ToImmutable();
            }
        }

        public bool TryGetString(int charOffset, out string value)
        {
            foreach (var entry in Strings)
            {
                if (entry.CharOffset == charOffset)
                {
                    value = entry.Value;
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }

        public HfsmRawSection GetSection(HfsmSectionKind kind)
        {
            var (start, end) = kind switch
            {
                HfsmSectionKind.StateData => (StateDataOffset, ActionDataOffset),
                HfsmSectionKind.ActionData => (ActionDataOffset, TransitionGraphOffset),
                HfsmSectionKind.TransitionGraph => (TransitionGraphOffset, TransitionInfoOffset),
                HfsmSectionKind.TransitionInfo => (TransitionInfoOffset, ConditionDataOffset),
                HfsmSectionKind.ConditionData => (ConditionDataOffset, SelectorDataOffset),
                HfsmSectionKind.SelectorData => (SelectorDataOffset, ExpressionDataOffset),
                HfsmSectionKind.ExpressionData => (ExpressionDataOffset, StringTableOffset),
                HfsmSectionKind.StringTable => (StringTableOffset, ExtraStringTableOffset),
                HfsmSectionKind.ExtraStringTable => (ExtraStringTableOffset, ActionReferenceTableOffset),
                HfsmSectionKind.ActionReferenceTable => (ActionReferenceTableOffset, UserVariablesOffset),
                HfsmSectionKind.UserVariables => (UserVariablesOffset, Data.Length),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };

            ValidateSection(kind, start, end);
            return new HfsmRawSection(kind, start, end - start, Data[start..end]);
        }

        private RszFile ReadRsz(HfsmSectionKind kind)
        {
            var section = GetSection(kind);
            return new RszFile(section.Data);
        }

        private ImmutableArray<HfsmStringTableEntry> ReadStringTable()
        {
            var section = GetSection(HfsmSectionKind.StringTable).Data.Span;
            if (section.Length < 4)
                return [];

            var declaredLength = ReadInt32(section, 0);
            return ReadNullTerminatedStrings(section[4..], declaredLength);
        }

        private ImmutableArray<HfsmStringTableEntry> ReadExtraStringTable()
        {
            var section = GetSection(HfsmSectionKind.ExtraStringTable).Data.Span;
            if (section.Length < 8)
                return [];

            var count = ReadInt32(section, 0);
            if (count == 0)
                return [];

            var declaredLength = ReadInt32(section, 4);
            return ReadNullTerminatedStrings(section[8..], declaredLength, count);
        }

        private static ImmutableArray<HfsmStringTableEntry> ReadNullTerminatedStrings(
            ReadOnlySpan<byte> bytes,
            int declaredLength,
            int maxCount = int.MaxValue)
        {
            var availableChars = bytes.Length / 2;
            var charCount = Math.Max(0, Math.Min(declaredLength, availableChars));
            var text = Encoding.Unicode.GetString(bytes[0..(charCount * 2)].ToArray());

            var result = ImmutableArray.CreateBuilder<HfsmStringTableEntry>();
            var start = 0;
            for (var i = 0; i <= text.Length && result.Count < maxCount; i++)
            {
                if (i != text.Length && text[i] != '\0')
                    continue;

                if (i != start)
                {
                    result.Add(new HfsmStringTableEntry(start, text[start..i]));
                }
                start = i + 1;
            }
            return result.ToImmutable();
        }

        private ImmutableArray<HfsmStateEntry> ReadStateEntries()
        {
            var section = GetSection(HfsmSectionKind.StateData).Data.Span;
            if (section.Length < 4)
                return [];

            var declaredCount = ReadInt32(section, 0);
            var pos = 4;
            var result = ImmutableArray.CreateBuilder<HfsmStateEntry>();
            if (declaredCount < 0)
                throw new InvalidDataException("HFSM state count is negative.");
            if (declaredCount > 0)
            {
                ReadStateEntry(section, ref pos, result, parentIndex: -1, depth: 0);
            }
            if (result.Count != declaredCount)
                throw new InvalidDataException($"HFSM state tree declared {declaredCount} states but contained {result.Count}.");
            return result.ToImmutable();
        }

        private int ReadStateEntry(
            ReadOnlySpan<byte> section,
            ref int pos,
            ImmutableArray<HfsmStateEntry>.Builder result,
            int parentIndex,
            int depth)
        {
            var start = pos;
            EnsureAvailable(section, pos, 28, HfsmSectionKind.StateData);
            var stateId = ReadUInt32(section, pos);
            pos += 4;
            var unknown4 = ReadInt32(section, pos);
            pos += 4;

            var tagCount = ReadInt32(section, pos);
            pos += 4;
            if (tagCount < 0)
                throw new InvalidDataException("HFSM state tag count is negative.");
            EnsureAvailable(section, pos, tagCount * 4, HfsmSectionKind.StateData);
            var tagIds = ImmutableArray.CreateBuilder<uint>();
            for (var i = 0; i < tagCount; i++)
            {
                tagIds.Add(ReadUInt32(section, pos));
                pos += 4;
            }

            var actionReferenceCount = ReadInt32(section, pos);
            pos += 4;
            if (actionReferenceCount < 0)
                throw new InvalidDataException("HFSM state action reference count is negative.");
            var actionReferences = ImmutableArray.CreateBuilder<HfsmStateActionReference>();
            for (var i = 0; i < actionReferenceCount; i++)
            {
                EnsureAvailable(section, pos, 5, HfsmSectionKind.StateData);
                actionReferences.Add(new HfsmStateActionReference(
                    ReadUInt32(section, pos),
                    section[pos + 4]));
                pos += 5;
            }

            EnsureAvailable(section, pos, 20, HfsmSectionKind.StateData);
            var transitionGroupIndex = ReadInt32(section, pos);
            pos += 4;
            var initialStateId = ReadUInt32(section, pos);
            pos += 4;
            var stateDataObjectCount = ReadInt32(section, pos);
            pos += 4;
            if (stateDataObjectCount < 0)
                throw new InvalidDataException("HFSM state data object count is negative.");
            var stateDataObjects = ImmutableArray.CreateBuilder<HfsmStateDataObject>();
            for (var i = 0; i < stateDataObjectCount; i++)
            {
                EnsureAvailable(section, pos, HfsmStateDataObject.Size, HfsmSectionKind.StateData);
                stateDataObjects.Add(new HfsmStateDataObject(
                    ReadUInt32(section, pos),
                    ReadUInt32(section, pos + 4),
                    ReadUInt32(section, pos + 8),
                    ReadUInt32(section, pos + 12),
                    ReadUInt32(section, pos + 16),
                    ReadUInt32(section, pos + 20),
                    section.Slice(pos, HfsmStateDataObject.Size).ToArray()));
                pos += HfsmStateDataObject.Size;
            }
            var nameCharOffset = ReadInt32(section, pos);
            pos += 4;
            TryGetString(nameCharOffset, out var name);
            var childCount = ReadInt32(section, pos);
            pos += 4;
            if (childCount < 0)
                throw new InvalidDataException("HFSM state child count is negative.");

            var index = result.Count;
            result.Add(new HfsmStateEntry(
                index,
                parentIndex,
                depth,
                start,
                stateId,
                unknown4,
                tagIds.ToImmutable(),
                actionReferences.ToImmutable(),
                transitionGroupIndex,
                initialStateId,
                stateDataObjects.ToImmutable(),
                nameCharOffset,
                name,
                []));

            var childIndices = ImmutableArray.CreateBuilder<int>();
            for (var i = 0; i < childCount; i++)
            {
                childIndices.Add(ReadStateEntry(section, ref pos, result, index, depth + 1));
            }

            result[index] = result[index].WithChildIndices(childIndices.ToImmutable());
            return index;
        }

        private ImmutableArray<HfsmTransitionGroup> ReadTransitionGroups()
        {
            var section = GetSection(HfsmSectionKind.TransitionGraph).Data.Span;
            if (section.Length < 4)
                return [];

            var pos = 0;
            var groupCount = ReadInt32(section, pos);
            pos += 4;
            var result = ImmutableArray.CreateBuilder<HfsmTransitionGroup>();
            for (var i = 0; i < groupCount && pos + 16 <= section.Length; i++)
            {
                var type = ReadUInt32(section, pos);
                var unknown0 = ReadUInt32(section, pos + 4);
                var declaredStateCount = ReadInt32(section, pos + 8);
                var stateCount = ReadInt32(section, pos + 12);
                pos += 16;

                var stateIds = ImmutableArray.CreateBuilder<HfsmStateId>();
                for (var j = 0; j < stateCount && pos + 8 <= section.Length; j++)
                {
                    stateIds.Add(new HfsmStateId(
                        ReadUInt32(section, pos),
                        ReadUInt32(section, pos + 4)));
                    pos += 8;
                }

                var transitions = ImmutableArray.CreateBuilder<HfsmTransition>();
                if (pos + 4 <= section.Length)
                {
                    var transitionCount = ReadInt32(section, pos);
                    pos += 4;
                    for (var j = 0; j < transitionCount && pos + 32 <= section.Length; j++)
                    {
                        transitions.Add(new HfsmTransition(
                            ReadUInt32(section, pos),
                            ReadUInt32(section, pos + 4),
                            ReadUInt32(section, pos + 8),
                            ReadUInt32(section, pos + 12),
                            ReadUInt32(section, pos + 16),
                            ReadUInt32(section, pos + 20),
                            ReadUInt32(section, pos + 24),
                            ReadUInt32(section, pos + 28)));
                        pos += 32;
                    }
                }

                result.Add(new HfsmTransitionGroup(
                    type,
                    unknown0,
                    declaredStateCount,
                    stateIds.ToImmutable(),
                    transitions.ToImmutable()));
            }
            return result.ToImmutable();
        }

        private ImmutableArray<HfsmTransitionInfo> ReadTransitionInfos()
        {
            var section = GetSection(HfsmSectionKind.TransitionInfo).Data.Span;
            if (section.Length < 4)
                return [];

            var count = ReadInt32(section, 0);
            var pos = 4;
            var result = ImmutableArray.CreateBuilder<HfsmTransitionInfo>();
            for (var i = 0; i < count && pos + 32 <= section.Length; i++)
            {
                var transitionId = ReadUInt32(section, pos);
                var conditionNameOffset = ReadInt32(section, pos + 4);
                TryGetString(conditionNameOffset, out var conditionName);
                result.Add(new HfsmTransitionInfo(
                    transitionId,
                    conditionNameOffset,
                    conditionName,
                    ReadInt32(section, pos + 8),
                    new Guid(section.Slice(pos + 12, 16).ToArray()),
                    section[pos + 28] != 0,
                    section[pos + 29] != 0,
                    (HfsmExpressionReferenceType)section[pos + 30],
                    section[pos + 31],
                    section.Slice(pos, 32).ToArray()));
                pos += 32;
            }
            return result.ToImmutable();
        }

        private ImmutableArray<HfsmActionReference> ReadActionReferences()
        {
            var section = GetSection(HfsmSectionKind.ActionReferenceTable).Data.Span;
            if (section.Length < 4)
                return [];

            var count = ReadInt32(section, 0);
            var pos = 4;
            var result = ImmutableArray.CreateBuilder<HfsmActionReference>();
            for (var i = 0; i < count && pos + 12 <= section.Length; i++)
            {
                result.Add(new HfsmActionReference(
                    ReadUInt32(section, pos),
                    ReadUInt32(section, pos + 4),
                    ReadInt32(section, pos + 8)));
                pos += 12;
            }
            return result.ToImmutable();
        }

        private ImmutableArray<HfsmState> BuildStates()
        {
            var entries = StateEntries
                .Where(x => x.StateId != 0)
                .GroupBy(x => x.StateId)
                .Select(x => x.First())
                .ToImmutableArray();
            if (entries.Length != 0)
            {
                return [.. entries.Select(x => new HfsmState(x.StateId, 0, x.NameCharOffset, x.Name))];
            }

            return [.. TransitionGroups
                .SelectMany(x => x.StateIds)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .Select(x => new HfsmState(x.Id, x.Unknown, -1, string.Empty))];
        }

        private void ValidateSection(HfsmSectionKind kind, int start, int end)
        {
            if (start < 0 || end < start || end > Data.Length)
            {
                throw new InvalidDataException(
                    $"HFSM section {kind} has invalid range 0x{start:X}..0x{end:X} for a 0x{Data.Length:X}-byte file.");
            }
        }

        private static void EnsureAvailable(ReadOnlySpan<byte> section, int offset, int size, HfsmSectionKind kind)
        {
            if (size < 0 || offset < 0 || offset + size > section.Length)
            {
                throw new InvalidDataException($"HFSM section {kind} ended unexpectedly at 0x{offset:X}.");
            }
        }

        private int ReadOffset(int offset)
        {
            var value = ReadInt64(offset);
            if (value < 0 || value > int.MaxValue)
                throw new InvalidDataException($"HFSM offset at 0x{offset:X} is out of range: 0x{value:X}.");
            return (int)value;
        }

        private uint ReadUInt32(int offset) => ReadUInt32(Data.Span, offset);
        private int ReadInt32(int offset) => ReadInt32(Data.Span, offset);
        private long ReadInt64(int offset) => BinaryPrimitives.ReadInt64LittleEndian(Data.Span.Slice(offset, 8));
        private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
            BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
    }

    public enum HfsmSectionKind
    {
        StateData,
        ActionData,
        TransitionGraph,
        TransitionInfo,
        ConditionData,
        SelectorData,
        ExpressionData,
        StringTable,
        ExtraStringTable,
        ActionReferenceTable,
        UserVariables,
    }

    public enum HfsmExpressionReferenceType : byte
    {
        LocalUserData = 0,
        GlobalUserData = 1,
        Direct = 2,
    }

    public sealed class HfsmRawSection(
        HfsmSectionKind kind,
        int offset,
        int size,
        ReadOnlyMemory<byte> data)
    {
        public HfsmSectionKind Kind { get; } = kind;
        public int Offset { get; } = offset;
        public int Size { get; } = size;
        public ReadOnlyMemory<byte> Data { get; } = data;
    }

    public sealed class HfsmStringTableEntry(int charOffset, string value)
    {
        public int CharOffset { get; } = charOffset;
        public string Value { get; } = value;
    }

    public sealed class HfsmState(uint id, uint unknown, int nameCharOffset, string name)
    {
        public uint Id { get; } = id;
        public uint Unknown { get; } = unknown;
        public int NameCharOffset { get; } = nameCharOffset;
        public string Name { get; } = name;
    }

    public sealed class HfsmStateEntry(
        int index,
        int parentIndex,
        int depth,
        int offset,
        uint stateId,
        int unknown4,
        ImmutableArray<uint> tagIds,
        ImmutableArray<HfsmStateActionReference> actionReferences,
        int transitionGroupIndex,
        uint initialStateId,
        ImmutableArray<HfsmStateDataObject> stateDataObjects,
        int nameCharOffset,
        string name,
        ImmutableArray<int> childIndices)
    {
        public int Index { get; } = index;
        public int ParentIndex { get; } = parentIndex;
        public int Depth { get; } = depth;
        public int Offset { get; } = offset;
        public uint StateId { get; } = stateId;
        public int Unknown4 { get; } = unknown4;
        public ImmutableArray<uint> TagIds { get; } = tagIds;
        public ImmutableArray<HfsmStateActionReference> ActionReferences { get; } = actionReferences;
        public int TransitionGroupIndex { get; } = transitionGroupIndex;
        public uint InitialStateId { get; } = initialStateId;
        public ImmutableArray<HfsmStateDataObject> StateDataObjects { get; } = stateDataObjects;
        public int StateDataObjectCount => StateDataObjects.Length;
        public int NameCharOffset { get; } = nameCharOffset;
        public string Name { get; } = name;
        public ImmutableArray<int> ChildIndices { get; } = childIndices;

        internal HfsmStateEntry WithChildIndices(ImmutableArray<int> value) =>
            new(Index, ParentIndex, Depth, Offset, StateId, Unknown4, TagIds, ActionReferences, TransitionGroupIndex,
                InitialStateId, StateDataObjects, NameCharOffset, Name, value);
    }

    public sealed class HfsmStateDataObject(
        uint stateId,
        uint stateUnknown,
        uint transitionId,
        uint transitionUnknown,
        uint unknown0,
        uint unknown1,
        byte[] rawData)
    {
        public const int Size = 24;

        public uint StateId { get; } = stateId;
        public uint StateUnknown { get; } = stateUnknown;
        public uint TransitionId { get; } = transitionId;
        public uint TransitionUnknown { get; } = transitionUnknown;
        public uint Unknown0 { get; } = unknown0;
        public uint Unknown1 { get; } = unknown1;
        public byte[] RawData { get; } = rawData;
    }

    public sealed class HfsmStateActionReference(uint uid, byte listNo)
    {
        public uint Uid { get; } = uid;
        public byte ListNo { get; } = listNo;
        public ulong Key => Uid | ((ulong)ListNo << 32);
    }

    public sealed class HfsmStateId(uint id, uint unknown)
    {
        public uint Id { get; } = id;
        public uint Unknown { get; } = unknown;
    }

    public sealed class HfsmTransitionGroup(
        uint type,
        uint unknown0,
        int declaredStateCount,
        ImmutableArray<HfsmStateId> stateIds,
        ImmutableArray<HfsmTransition> transitions)
    {
        public uint Type { get; } = type;
        public uint Unknown0 { get; } = unknown0;
        public int DeclaredStateCount { get; } = declaredStateCount;
        public ImmutableArray<HfsmStateId> StateIds { get; } = stateIds;
        public ImmutableArray<HfsmTransition> Transitions { get; } = transitions;
    }

    public sealed class HfsmTransition(
        uint fromStateId,
        uint fromStateUnknown,
        uint toStateId,
        uint toStateUnknown,
        uint transitionId,
        uint transitionUnknown,
        uint unknown0,
        uint unknown1)
    {
        public uint FromStateId { get; } = fromStateId;
        public uint FromStateUnknown { get; } = fromStateUnknown;
        public uint ToStateId { get; } = toStateId;
        public uint ToStateUnknown { get; } = toStateUnknown;
        public uint TransitionId { get; } = transitionId;
        public uint TransitionUnknown { get; } = transitionUnknown;
        public uint Unknown0 { get; } = unknown0;
        public uint Unknown1 { get; } = unknown1;
    }

    public sealed class HfsmTransitionInfo(
        uint transitionId,
        int conditionNameCharOffset,
        string conditionName,
        int conditionObjectIndex,
        Guid expression,
        bool enabled,
        bool condition,
        HfsmExpressionReferenceType expressionReferenceType,
        byte padding,
        byte[] rawData)
    {
        public uint TransitionId { get; } = transitionId;
        public int ConditionNameCharOffset { get; } = conditionNameCharOffset;
        public string ConditionName { get; } = conditionName;
        public int ConditionObjectIndex { get; } = conditionObjectIndex;
        public Guid Expression { get; } = expression;
        public bool Enabled { get; } = enabled;
        public bool Condition { get; } = condition;
        public HfsmExpressionReferenceType ExpressionReferenceType { get; } = expressionReferenceType;
        public byte Padding { get; } = padding;
        public byte[] RawData { get; } = rawData;
    }

    public sealed class HfsmActionReference(uint uid, uint listNo, int objectIndex)
    {
        public uint Uid { get; } = uid;
        public uint ListNo { get; } = listNo;
        public int ObjectIndex { get; } = objectIndex;
        public ulong Key => Uid | ((ulong)ListNo << 32);
    }
}
