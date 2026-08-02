using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// A node in a behavior tree / FSM (.fsmv2, .bhvt). Every reference beyond the parent/child hierarchy
    /// (state and transition targets) is by <see cref="BhvtNodeId"/> rather than by embedding, since those
    /// can point anywhere in the tree, not just at structural children.
    /// </summary>
    public sealed class BhvtNode
    {
        public BhvtNode(
            BhvtNodeId id,
            string name,
            BhvtNodeAttributes attributes,
            int priority,
            bool isBranch,
            bool isEnd,
            BhvtWorkFlags workFlags,
            uint nameHash,
            uint fullNameHash,
            ImmutableArray<uint> tags,
            RszObjectNode? selector,
            RszObjectNode? selectorCallerCondition,
            ImmutableArray<RszObjectNode> selectorCallers,
            ImmutableArray<BhvtAction> actions,
            ImmutableArray<BhvtChild> children,
            ImmutableArray<BhvtState> states,
            ImmutableArray<BhvtTransition> transitions,
            ImmutableArray<BhvtAllState> allStates,
            string? referenceTree)
        {
            Id = id;
            Name = name;
            Attributes = attributes;
            Priority = priority;
            IsBranch = isBranch;
            IsEnd = isEnd;
            WorkFlags = workFlags;
            NameHash = nameHash;
            FullNameHash = fullNameHash;
            Tags = tags;
            Selector = selector;
            SelectorCallerCondition = selectorCallerCondition;
            SelectorCallers = selectorCallers;
            Actions = actions;
            Children = children;
            States = states;
            Transitions = transitions;
            AllStates = allStates;
            ReferenceTree = referenceTree;
        }

        public BhvtNodeId Id { get; }
        public string Name { get; }
        public BhvtNodeAttributes Attributes { get; }
        public int Priority { get; }
        public bool IsBranch { get; }
        public bool IsEnd { get; }

        /// <summary>Runtime work-in-progress flags. Preserved for fidelity; not meaningful to author by hand.</summary>
        public BhvtWorkFlags WorkFlags { get; }

        /// <summary>Hash of <see cref="Name"/>. Preserved as-is rather than recomputed on write.</summary>
        public uint NameHash { get; }

        /// <summary>Hash of the node's full dotted path name. Preserved as-is rather than recomputed on write.</summary>
        public uint FullNameHash { get; }

        public ImmutableArray<uint> Tags { get; }

        public RszObjectNode? Selector { get; }
        public RszObjectNode? SelectorCallerCondition { get; }
        public ImmutableArray<RszObjectNode> SelectorCallers { get; }
        public ImmutableArray<BhvtAction> Actions { get; }
        public ImmutableArray<BhvtChild> Children { get; }
        public ImmutableArray<BhvtState> States { get; }
        public ImmutableArray<BhvtTransition> Transitions { get; }
        public ImmutableArray<BhvtAllState> AllStates { get; }

        /// <summary>Path to another .bhvt tree this node embeds, if <see cref="BhvtNodeAttributes.HasReferenceTree"/> is set.</summary>
        public string? ReferenceTree { get; }

        public BhvtNode With(
            BhvtNodeId? id = null,
            string? name = null,
            BhvtNodeAttributes? attributes = null,
            int? priority = null,
            bool? isBranch = null,
            bool? isEnd = null,
            BhvtWorkFlags? workFlags = null,
            uint? nameHash = null,
            uint? fullNameHash = null,
            ImmutableArray<uint>? tags = null,
            RszObjectNode? selector = null,
            bool clearSelector = false,
            RszObjectNode? selectorCallerCondition = null,
            bool clearSelectorCallerCondition = false,
            ImmutableArray<RszObjectNode>? selectorCallers = null,
            ImmutableArray<BhvtAction>? actions = null,
            ImmutableArray<BhvtChild>? children = null,
            ImmutableArray<BhvtState>? states = null,
            ImmutableArray<BhvtTransition>? transitions = null,
            ImmutableArray<BhvtAllState>? allStates = null,
            string? referenceTree = null,
            bool clearReferenceTree = false)
        {
            return new BhvtNode(
                id ?? Id,
                name ?? Name,
                attributes ?? Attributes,
                priority ?? Priority,
                isBranch ?? IsBranch,
                isEnd ?? IsEnd,
                workFlags ?? WorkFlags,
                nameHash ?? NameHash,
                fullNameHash ?? FullNameHash,
                tags ?? Tags,
                clearSelector ? null : selector ?? Selector,
                clearSelectorCallerCondition ? null : selectorCallerCondition ?? SelectorCallerCondition,
                selectorCallers ?? SelectorCallers,
                actions ?? Actions,
                children ?? Children,
                states ?? States,
                transitions ?? Transitions,
                allStates ?? AllStates,
                clearReferenceTree ? null : referenceTree ?? ReferenceTree);
        }

        public BhvtNode WithName(string name) => With(name: name);
        public BhvtNode WithChildren(ImmutableArray<BhvtChild> children) => With(children: children);
        public BhvtNode WithActions(ImmutableArray<BhvtAction> actions) => With(actions: actions);
        public BhvtNode WithStates(ImmutableArray<BhvtState> states) => With(states: states);
        public BhvtNode WithTransitions(ImmutableArray<BhvtTransition> transitions) => With(transitions: transitions);
        public BhvtNode WithAllStates(ImmutableArray<BhvtAllState> allStates) => With(allStates: allStates);

        public override string ToString() => string.IsNullOrEmpty(Name) ? $"[{Id}]" : Name;
    }
}
