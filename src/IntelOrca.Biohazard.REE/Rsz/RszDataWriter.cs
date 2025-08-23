using System;
using System.Collections.Generic;
using System.IO;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal class RszDataWriter
    {
        private readonly Stream _stream;
        private readonly Dictionary<IRszNode, RszInstanceId> _instanceMap;
        private readonly BinaryWriter _bw;
        private readonly long _baseAddress;

        public int BytesWritten => (int)(_stream.Position - _baseAddress);

        public RszDataWriter(Stream stream, Dictionary<IRszNode, RszInstanceId> instanceMap)
        {
            _stream = stream;
            _instanceMap = instanceMap;
            _bw = new BinaryWriter(stream);
            _baseAddress = stream.Position;
        }

        private void Align(int align)
        {
            var address = _stream.Position - _baseAddress;
            var mask = align - 1;
            var rem = (int)(address & mask);
            if (rem != 0)
            {
                _bw.Skip(align - rem);
            }
        }

        public void Write(IRszNode node)
        {
            if (node is RszStructNode structNode)
            {
                var rszType = structNode.Type;
                for (var i = 0; i < rszType.Fields.Length; i++)
                {
                    var field = rszType.Fields[i];
                    if (field.IsArray)
                    {
                        var arrayNode = (RszArrayNode)structNode.Children[i];
                        Align(4);
                        _bw.Write(arrayNode.Children.Length);
                        if (arrayNode.Children.Length > 0)
                        {
                            Align(field.Align);
                        }
                        for (var j = 0; j < arrayNode.Children.Length; j++)
                        {
                            var child = arrayNode.Children[j];
                            if (_instanceMap.TryGetValue(child, out var instanceId))
                            {
                                _bw.Write(instanceId.Index);
                            }
                            else
                            {
                                Write(child);
                            }
                        }
                    }
                    else
                    {
                        var child = structNode.Children[i];

                        Align(field.Align);
                        if (field.Type == RszFieldType.Object || field.Type == RszFieldType.UserData)
                        {
                            if (_instanceMap.TryGetValue(child, out var instanceId))
                            {
                                _bw.Write(instanceId.Index);
                            }
                            else
                            {
                                throw new Exception($"No instance in map found for {field.Name}.");
                            }
                        }
                        else if (field.Type == RszFieldType.String || field.Type == RszFieldType.Resource)
                        {
                            Write(child);
                        }
                        else
                        {
                            var oldPosition = _bw.BaseStream.Position;
                            Write(child);
                            var newPosition = _bw.BaseStream.Position;
                            var bytesWritten = (int)(newPosition - oldPosition);
                            if (bytesWritten < field.Size)
                            {
                                _bw.Seek(field.Size - bytesWritten, SeekOrigin.Current);
                            }
                            else if (bytesWritten > field.Size)
                            {
                                throw new Exception($"{field.Name} is {field.Size} bytes, but {bytesWritten} was written.");
                            }
                        }
                    }
                }
            }
            else if (node is RszStringNode stringNode)
            {
                Align(4);
                _bw.Write(stringNode.Value.Length + 1);
                foreach (var ch in stringNode.Value)
                {
                    _bw.Write((short)ch);
                }
                _bw.Write((short)0);
            }
            else if (node is RszResourceNode resourceNode)
            {
                Align(4);
                if (string.IsNullOrEmpty(resourceNode.Value))
                {
                    _bw.Write(0);
                }
                else
                {
                    _bw.Write(resourceNode.Value.Length + 1);
                    foreach (var ch in resourceNode.Value)
                    {
                        _bw.Write((short)ch);
                    }
                    _bw.Write((short)0);
                }
            }
            else if (node is RszDataNode dataNode)
            {
                _bw.Write(dataNode.Data.Span);
            }
        }
    }
}
