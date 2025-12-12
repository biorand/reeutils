using System;
using System.Collections;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszObjectNode : IRszNodeContainer
    {
        public RszType Type { get; }
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszObjectNode(RszType type, ImmutableArray<IRszNode> children)
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
                    throw new ArgumentException($"{fieldName} is not a field of {Type.Name}.");

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

        public RszObjectNode SetField(string name, IRszNode value)
        {
            var index = Type.FindFieldIndex(name);
            if (index == -1)
                throw new ArgumentException($"{0} is not a field of {Type.Name}.");

            return new RszObjectNode(Type, Children.SetItem(index, value));
        }

        public RszObjectNode SetField(string name, object? value)
        {
            if (value is IRszNode node)
                return SetField(name, node);

            var index = Type.FindFieldIndex(name);
            if (index == -1)
                throw new ArgumentException($"{name} is not a field of {Type.Name}.");

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
                        children.Add(RszSerializer.Serialize(field.Type, item, Type.Repository));
                    }
                }
                return new RszObjectNode(Type, Children.SetItem(index, new RszArrayNode(field.Type, children.ToImmutable())));
            }

            return new RszObjectNode(
                Type,
                Children.SetItem(index, field.Type == RszFieldType.Object
                    ? RszSerializer.Serialize(field.ObjectType!, value)
                    : RszSerializer.Serialize(field.Type, value)));
        }

        public RszObjectNode WithChildren(ImmutableArray<IRszNode> children) => new RszObjectNode(Type, children);

        IRszNodeContainer IRszNodeContainer.WithChildren(ImmutableArray<IRszNode> children) => WithChildren(children);

        public override string ToString() => Type.Name;
    }
}
