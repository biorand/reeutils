using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public interface IRszNode
    {
        public ImmutableArray<IRszNode> Children { get; set; }
    }
}
