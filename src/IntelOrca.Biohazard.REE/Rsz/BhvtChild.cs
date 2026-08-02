namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>A parent/child edge, with the (optional) condition gating traversal onto that child.</summary>
    public sealed class BhvtChild(BhvtNode node, RszObjectNode? condition)
    {
        public BhvtNode Node { get; } = node;
        public RszObjectNode? Condition { get; } = condition;

        public BhvtChild WithNode(BhvtNode node) => new(node, Condition);
        public BhvtChild WithCondition(RszObjectNode? condition) => new(Node, condition);

        public override string ToString() => Node.Name;
    }
}
