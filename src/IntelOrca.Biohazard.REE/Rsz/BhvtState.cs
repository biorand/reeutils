using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>A state transition available while a node's action(s) are still running.</summary>
    public sealed class BhvtState(
        BhvtNodeId target,
        RszObjectNode? condition,
        uint transitionMapId,
        uint stateEx,
        ImmutableArray<RszObjectNode> events)
    {
        public BhvtNodeId Target { get; } = target;
        public RszObjectNode? Condition { get; } = condition;
        public uint TransitionMapId { get; } = transitionMapId;
        public uint StateEx { get; } = stateEx;
        public ImmutableArray<RszObjectNode> Events { get; } = events;

        public BhvtState WithTarget(BhvtNodeId target) => new(target, Condition, TransitionMapId, StateEx, Events);
        public BhvtState WithCondition(RszObjectNode? condition) => new(Target, condition, TransitionMapId, StateEx, Events);
        public BhvtState WithEvents(ImmutableArray<RszObjectNode> events) => new(Target, Condition, TransitionMapId, StateEx, events);

        public override string ToString() => $"=> {Target}";
    }
}
