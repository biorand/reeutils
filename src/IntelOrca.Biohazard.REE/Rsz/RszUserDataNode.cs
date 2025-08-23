using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszUserDataNode(RszType type, string path) : IEquatable<RszUserDataNode>, IRszNode
    {
        public RszType Type => type;
        public string Path => path;

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new NotSupportedException();
        }

        public bool Equals(RszUserDataNode other) => other.Type == Type && other.Path == Path;
        public override bool Equals(object obj) => obj is RszUserDataNode node && Equals(node);
        public override int GetHashCode() => (Type.GetHashCode() * 13) ^ (Path.GetHashCode() * 23);

        public override string ToString() => $"Userdata({Path}): {Type.Name}";
    }

    public class RszEmbeddedUserDataNode(RszType type, int hash, RszFile embedded) : IRszNode
    {
        public RszType Type => type;
        public int Hash => hash;
        public RszFile Embedded => embedded;

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new NotSupportedException();
        }
    }
}
