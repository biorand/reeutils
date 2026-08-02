namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>A state transition available from anywhere below this node (not just while it's active).</summary>
    public sealed class BhvtAllState(BhvtNodeId target, RszObjectNode? condition, uint transitionMapId, uint transitionAttributes)
    {
        public BhvtNodeId Target { get; } = target;
        public RszObjectNode? Condition { get; } = condition;
        public uint TransitionMapId { get; } = transitionMapId;
        public uint TransitionAttributes { get; } = transitionAttributes;

        public BhvtAllState WithTarget(BhvtNodeId target) => new(target, Condition, TransitionMapId, TransitionAttributes);
        public BhvtAllState WithCondition(RszObjectNode? condition) => new(Target, condition, TransitionMapId, TransitionAttributes);

        public override string ToString() => $"=> {Target}";
    }
}
