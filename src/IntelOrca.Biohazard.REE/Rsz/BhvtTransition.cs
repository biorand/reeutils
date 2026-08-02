using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// A transition rooted at this node, optionally scoped to one specific child (<see cref="Start"/>)
    /// being the currently active one.
    /// </summary>
    public sealed class BhvtTransition(BhvtNodeId start, RszObjectNode? condition, ImmutableArray<uint> rawEvents)
    {
        public BhvtNodeId Start { get; } = start;
        public RszObjectNode? Condition { get; } = condition;

        /// <summary>Raw, unresolved event ids. Always empty/trivial in every file seen so far.</summary>
        public ImmutableArray<uint> RawEvents { get; } = rawEvents;

        public BhvtTransition WithStart(BhvtNodeId start) => new(start, Condition, RawEvents);
        public BhvtTransition WithCondition(RszObjectNode? condition) => new(Start, condition, RawEvents);

        public override string ToString() => $"=> {Start}";
    }
}
