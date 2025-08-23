using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszArrayNode : IRszNode, IEnumerable<IRszNode>
    {
        public RszFieldType Type { get; set; }
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszArrayNode(RszFieldType type, ImmutableArray<IRszNode> children)
        {
            Type = type;
            Children = children;
        }

        public int Length => Children.Length;

        public IRszNode this[int index]
        {
            get => Children[index];
            set => Children = Children.SetItem(index, value);
        }

        public RszArrayNode Add(IRszNode node)
        {
            return new RszArrayNode(Type, Children.Add(node));
        }

        public RszArrayNode Add(object value)
        {
            return Add(RszSerializer.Serialize(Type, value));
        }

        public RszArrayNode AddRange(IEnumerable<IRszNode> nodes)
        {
            return new RszArrayNode(Type, Children.AddRange(nodes));
        }

        public RszArrayNode AddRange(IEnumerable<object> values)
        {
            return AddRange(values.Select(x => RszSerializer.Serialize(Type, x)));
        }

        public RszArrayNode SetItem(int index, IRszNode node)
        {
            return new RszArrayNode(Type, Children.SetItem(index, node));
        }

        public RszArrayNode SetItem(int index, object value)
        {
            if (value is IRszNode node)
                return SetItem(index, node);
            return new RszArrayNode(Type, Children.SetItem(index, RszSerializer.Serialize(Type, value)));
        }

        public RszArrayNode RemoveAt(int index)
        {
            return new RszArrayNode(Type, Children.RemoveAt(index));
        }

        public IEnumerator<IRszNode> GetEnumerator() => ((IEnumerable<IRszNode>)Children).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
