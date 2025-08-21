using System;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszStructNode : IRszNode
    {
        public RszType Type { get; }
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszStructNode(RszType type, ImmutableArray<IRszNode> children)
        {
            Type = type;
            Children = children;
        }

        public IRszNode this[int index]
        {
            get => Children[index];
            set
            {
                var field = Type.Fields[index];
                if (!Validate(field, value))
                    throw new Exception($"Invalid value for field {field.Name}. Expected {field.Type}.");
                Children = Children.SetItem(index, value);
            }
        }

        public IRszNode this[string fieldName]
        {
            get
            {
                var index = Type.FindFieldIndex(fieldName);
                if (index == -1)
                    throw new ArgumentException($"{0} is not a field of {Type.Name}.");

                return Children[index];
            }
            set
            {
                var index = Type.FindFieldIndex(fieldName);
                if (index == -1)
                    throw new ArgumentException($"{0} is not a field of {Type.Name}.");

                this[index] = value;
            }
        }

        private bool Validate(RszTypeField field, IRszNode node)
        {
            // TODO
            return true;
        }

        public override string ToString() => Type.Name.ToString();
    }
}
