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
}
