using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// Identifies a <see cref="BhvtNode"/>: a 32-bit id plus a secondary "extra id" disambiguator.
    /// Used both as a node's own identity and to reference another node (state/transition targets)
    /// without embedding it, since those references aren't restricted to the parent/child hierarchy.
    /// </summary>
    public readonly struct BhvtNodeId(uint id, uint exId) : IEquatable<BhvtNodeId>
    {
        public uint Id { get; } = id;
        public uint ExId { get; } = exId;

        public static readonly BhvtNodeId Unset = new(uint.MaxValue, 0);

        public bool IsUnset => Id == uint.MaxValue;
        internal ulong Packed => Id | ((ulong)ExId << 32);

        public bool Equals(BhvtNodeId other) => Packed == other.Packed;
        public override bool Equals(object? obj) => obj is BhvtNodeId other && Equals(other);
        public override int GetHashCode() => Packed.GetHashCode();
        public static bool operator ==(BhvtNodeId left, BhvtNodeId right) => left.Equals(right);
        public static bool operator !=(BhvtNodeId left, BhvtNodeId right) => !left.Equals(right);

        public override string ToString() => $"{Id} ({ExId})";
    }
}
