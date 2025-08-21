using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszUserDataNode(RszType type, string path) : IRszNode
    {
        public RszType Type => type;
        public string Path => path;

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new NotSupportedException();
        }
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
