using System;
using System.Collections;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszStructNode : IRszNodeContainer
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

        public RszStructNode SetField(string name, IRszNode value)
        {
            var index = Type.FindFieldIndex(name);
            if (index == -1)
                throw new ArgumentException($"{0} is not a field of {Type.Name}.");

            return new RszStructNode(Type, Children.SetItem(index, value));
        }

        public RszStructNode SetField(string name, object value)
        {
            if (value is IRszNode node)
                return SetField(name, node);

            var index = Type.FindFieldIndex(name);
            if (index == -1)
                throw new ArgumentException($"{0} is not a field of {Type.Name}.");

            var field = Type.Fields[index];
            if (value is IList list)
            {
                var children = ImmutableArray.CreateBuilder<IRszNode>();
                foreach (var item in list)
                {
                    if (item is IRszNode itemNode)
                    {
                        children.Add(itemNode);
                    }
                    else if (field.Type == RszFieldType.Object)
                    {
                        children.Add(RszSerializer.Serialize(field.ObjectType!, item));
                    }
                    else
                    {
                        children.Add(RszSerializer.Serialize(field.Type, item));
                    }
                }
                return new RszStructNode(Type, Children.SetItem(index, new RszArrayNode(field.Type, children.ToImmutable())));
            }

            return new RszStructNode(
                Type,
                Children.SetItem(index, field.Type == RszFieldType.Object
                    ? RszSerializer.Serialize(field.ObjectType!, value)
                    : RszSerializer.Serialize(field.Type, value)));
        }

        public RszStructNode WithChildren(ImmutableArray<IRszNode> children) => new RszStructNode(Type, children);

        IRszNodeContainer IRszNodeContainer.WithChildren(ImmutableArray<IRszNode> children) => WithChildren(children);

        public override string ToString() => Type.Name;
    }
}
