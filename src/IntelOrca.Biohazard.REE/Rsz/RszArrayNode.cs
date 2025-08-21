using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszArrayNode : IRszNode
    {
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszArrayNode(ImmutableArray<IRszNode> children)
        {
            Children = children;
        }
    }
}
