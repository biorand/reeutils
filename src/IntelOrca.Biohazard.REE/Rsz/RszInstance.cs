﻿using System;
using System.Buffers.Binary;
using System.Collections.Immutable;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszInstance : IRszNode
    {
        public RszInstanceId Id { get; set; }
        public IRszNode? Value { get; set; }

        public ImmutableArray<IRszNode> Children
        {
            get => Value?.Children ?? [];
            set => Value!.Children = value;
        }

        public override string ToString() => $"{Value}[{Id.Index}]";
    }

    public interface IRszNode
    {
        public ImmutableArray<IRszNode> Children { get; set; }
    }

    public readonly struct RszDataNode : IRszNode
    {
        public RszFieldType Type { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new InvalidOperationException();
        }

        public RszDataNode(RszFieldType type, ReadOnlyMemory<byte> data)
        {
            Type = type;
            Data = data;
        }

        public int AsInt32() => BinaryPrimitives.ReadInt32LittleEndian(Data.Span);

        public override string ToString()
        {
            return Data.Length switch
            {
                1 => Data.Span[0].ToString(),
                2 => BinaryPrimitives.ReadInt16LittleEndian(Data.Span).ToString(),
                4 => BinaryPrimitives.ReadInt32LittleEndian(Data.Span).ToString(),
                8 => BinaryPrimitives.ReadInt64LittleEndian(Data.Span).ToString(),
                _ => $"{Data.Length} bytes"
            };
        }
    }

    public readonly struct RszStringNode : IRszNode
    {
        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new InvalidOperationException();
        }
        public string Value { get; }

        public RszStringNode(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;
    }

    public class RszArrayNode : IRszNode
    {
        public ImmutableArray<IRszNode> Children { get; set; }

        public RszArrayNode(ImmutableArray<IRszNode> children)
        {
            Children = children;
        }
    }

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
            set => Children = Children.SetItem(index, value);
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
        }

        public override string ToString() => Type.Name.ToString();
    }

    public class RszUserDataNode : IRszNode
    {
        public RszType Type { get; }

        public ImmutableArray<IRszNode> Children
        {
            get => [];
            set => throw new NotSupportedException();
        }

        public RszUserDataNode(RszType type)
        {
            Type = type;
        }
    }

    public interface IRszSceneNode : IRszNode
    {
    }

    public sealed class RszScene : IRszSceneNode
    {
        private ImmutableArray<IRszSceneNode> _children = [];

        public RszScene(ImmutableArray<IRszSceneNode> children)
        {
            _children = children;
        }

        public ImmutableArray<IRszSceneNode> Children
        {
            get => _children;
            set => _children = value;
        }

        ImmutableArray<IRszNode> IRszNode.Children
        {
            get => _children.CastArray<IRszNode>();
            set => _children = value.CastArray<IRszSceneNode>();
        }
    }

    public sealed class RszFolder : IRszSceneNode
    {
        private RszStructNode _settings;
        private ImmutableArray<IRszSceneNode> _children = [];

        public RszFolder(RszStructNode settings, ImmutableArray<IRszSceneNode> children)
        {
            _settings = settings;
            _children = children;
        }

        public RszStructNode Settings
        {
            get => _settings;
            set
            {
                if (value?.Type.Name != "via.Folder")
                {
                    throw new ArgumentException("Settings must be of type via.Folder.");
                }
                _settings = value;
            }
        }

        public ImmutableArray<IRszSceneNode> Children
        {
            get => _children;
            set => _children = value;
        }

        ImmutableArray<IRszNode> IRszNode.Children
        {
            get => _children.CastArray<IRszNode>();
            set => _children = value.CastArray<IRszSceneNode>();
        }

        public string Name => ((RszStringNode)_settings[0]).Value;

        public override string ToString() => Name;
    }

    public sealed class RszGameObject : IRszSceneNode
    {
        private RszStructNode _settings;

        public RszGameObject(Guid guid, RszStructNode settings, ImmutableArray<IRszNode> components, ImmutableArray<RszGameObject> children)
        {
            Guid = guid;
            _settings = settings;
            Components = components;
            Children = children;
        }

        public Guid Guid { get; set; }

        public RszStructNode Settings
        {
            get => _settings;
            set
            {
                if (value?.Type.Name != "via.GameObject")
                {
                    throw new ArgumentException("Settings must be of type via.GameObject.");
                }
                _settings = value;
            }
        }

        public ImmutableArray<IRszNode> Components { get; set; }

        public ImmutableArray<RszGameObject> Children { get; set; }

        ImmutableArray<IRszNode> IRszNode.Children
        {
            get => Children.CastArray<IRszNode>();
            set => Children = value.CastArray<RszGameObject>();
        }

        public string Name => ((RszStringNode)_settings[0]).Value;

        public override string ToString() => Name;
    }
}
