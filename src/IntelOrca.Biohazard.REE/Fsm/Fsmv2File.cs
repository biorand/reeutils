using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using IntelOrca.Biohazard.REE.Extensions;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Fsm
{
    public sealed class Fsmv2File(int version, ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x54564842;

        public ReadOnlyMemory<byte> Data => data;

        private uint Magic => BinaryPrimitives.ReadUInt32LittleEndian(data.Span);
        public ReadOnlySpan<ulong> Offsets => MemoryMarshal.Cast<byte, ulong>(data.Span.Slice(8, 18 * 8));

        private ReadOnlyMemory<byte> GetDataAtOffset(OffsetKind kind) => Data.Slice((int)Offsets[(int)kind]);

        private ReadOnlyMemory<byte> NodeData => GetDataAtOffset(OffsetKind.Node);

        public int NumStates => BinaryPrimitives.ReadInt32LittleEndian(NodeData.Span);
        public ImmutableArray<string> StateNames
        {
            get
            {
                var result = ImmutableArray.CreateBuilder<string>();
                var table = MemoryMarshal.Cast<byte, char>(GetDataAtOffset(OffsetKind.Name).Span);
                var offset = 0;

                var sb = new StringBuilder();
                var count = NumStates;
                for (var i = 0; i < count; i++)
                {
                    while (true)
                    {
                        var ch = table[offset++];
                        if (ch == 0)
                            break;

                        sb.Append(ch);
                    }
                    result.Add(sb.ToString());
                    sb.Clear();
                }

                return result.ToImmutable();
            }
        }

        private string ReadName(int charIndex)
        {
            var nameData = MemoryMarshal.Cast<byte, char>(GetDataAtOffset(OffsetKind.Name).Slice(4).Span).Slice(charIndex);
            var length = 0;
            while (nameData[length] != 0)
            {
                length++;
            }
            return Encoding.Unicode.GetString(MemoryMarshal.Cast<char, byte>(nameData.Slice(0, length)));
        }

        public void ReadCoreData()
        {
            var nodeData = NodeData.ToArray();
            var br = new BinaryReader(new MemoryStream(nodeData));
            var totalNodeCount = br.ReadInt32();

            var nodes = new List<BHVTNode>();
            for (var i = 0; i < totalNodeCount; i++)
            {
                var node = new BHVTNode()
                {
                    Id = br.ReadUInt32(),
                    ExId = br.ReadUInt32(),
                    Name = br.ReadInt32(),
                    Parent = br.ReadUInt32(),
                    ParentEx = br.ReadUInt32(),
                    Children = ReadArray(c =>
                    {
                        var id = br.ReadArray<uint>(c);
                        var idEx = br.ReadArray<uint>(c);
                        var condition = br.ReadArray<BHVTId>(c);
                        return Enumerable.Range(0, c)
                            .Select(x => new BHVTChild()
                            {
                                Id = id[x],
                                IdEx = idEx[x],
                                Condition = condition[x]
                            })
                            .ToArray();
                    }),
                    SelectorId = br.Read<BHVTId>(),
                    SelectorCallersCount = br.ReadInt32(),
                    SelectorCallerConditionId = br.Read<BHVTId>(),
                    Actions = ReadArray(br.ReadArray<ActionContainer>),
                    Priority = br.ReadInt32(),
                    NodeAttributes = br.Read<NodeAttributes>(),
                    TagsCount = br.ReadInt32(),
                    IsBranch = br.ReadBoolean(),
                    IsEnd = br.ReadBoolean(),
                    States = ReadArray(br.ReadArray<State>),
                    Transitions = ReadArray(br.ReadArray<Transition>),
                    AllStateCount = br.ReadInt32(),
                    ReferenceTreeIndex = br.ReadInt32()
                };
                nodes.Add(node);
            }

            var fsmNodes = nodes.Select(x => new FsmNode()
            {
                Id = x.Id,
                Name = ReadName(x.Name)
            }).ToArray();
            var fsmDict = fsmNodes.ToDictionary(x => x.Id);

            for (var i = 0; i < fsmNodes.Length; i++)
            {
                var node = nodes[i];
                var fsmNode = fsmNodes[i];
                var parent = fsmDict[fsmNode.Id];
                if (parent != fsmNode)
                    fsmNode.Parent = fsmNode;

                fsmNode.Children = node.Children.Select(x => fsmDict[x.Id]).ToImmutableArray();
            }

            ImmutableArray<T> ReadArray<T>(Func<int, T[]> cb)
            {
                var count = br.ReadInt32();
                return cb(count).ToImmutableArray();
            }
        }

        public RszFile GetRsz(OffsetKind kind) => new RszFile(GetDataAtOffset(kind));

        public enum OffsetKind
        {
            Node,
            Action,
            Selector,
            SelectorCaller,
            Conditions,
            TransitionEvent,
            ExpressionTreeConditions,
            StaticAction,
            StaticSelectorCaller,
            StaticConditions,
            StaticTransitionEvent,
            StaticExpressionTreeConditions,
            Name,
            ResourcePaths,
            UserdataPaths,
            Variable,
            BaseVariable,
            ReferencePrefabGameObjects,
        }

        private class BHVTNode
        {
            public uint Id { get; init; }
            public uint ExId { get; init; }
            public int Name { get; init; }
            public uint Parent { get; init; }
            public uint ParentEx { get; init; }
            public ImmutableArray<BHVTChild> Children { get; init; }
            public BHVTId SelectorId { get; init; }
            public int SelectorCallersCount { get; init; }
            public BHVTId SelectorCallerConditionId { get; init; }
            public ImmutableArray<ActionContainer> Actions { get; init; }
            public int Priority { get; init; }
            public NodeAttributes NodeAttributes { get; init; }
            public int TagsCount { get; set; }
            public bool IsBranch { get; init; }
            public bool IsEnd { get; init; }
            public ImmutableArray<State> States { get; init; }
            public ImmutableArray<Transition> Transitions { get; init; }
            public int AllStateCount { get; init; }
            public int ReferenceTreeIndex { get; init; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BHVTId
        {
            public ushort id;
            public byte ukn;
            public byte idType;
        }

        private struct BHVTChild
        {
            public uint Id { get; init; }
            public uint IdEx { get; init; }
            public BHVTId Condition { get; init; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NodeAttributes
        {
            public ushort NodeAttribute;
            public ushort WorkFlags;
            public uint NameHash;
            public uint FullnameHash;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct State
        {
            public BHVTId States;
            public uint Transitions;
            public BHVTId TransitionConditions;
            public uint TransitionMaps;
            public uint TransitionAttributes;
            public uint StatesEx;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ActionContainer
        {
            public uint Action;
            public uint ActionEx;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Transition
        {
            public BHVTId StartTransitionEvent;
            public uint StartState;
            public BHVTId StartStateTransition;
            public uint StartStateEx;
        }
    }

    [DebuggerDisplay("{Name}")]
    public class FsmNode
    {
        public required uint Id { get; init; }
        public required string Name { get; init; }
        public FsmNode? Parent { get; set; }
        public ImmutableArray<FsmNode> Children { get; set; }

        public override string ToString() => Name;
    }
}
