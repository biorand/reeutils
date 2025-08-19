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

        public RszDataWriter(Stream stream, Dictionary<IRszNode, RszInstanceId> instanceMap)
        {
            _stream = stream;
            _instanceMap = instanceMap;
            _bw = new BinaryWriter(stream);
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
                        _bw.Align(4);
                        _bw.Write(arrayNode.Children.Length);
                        if (arrayNode.Children.Length > 0)
                        {
                            _bw.Align(field.Align);
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
                        if (_instanceMap.TryGetValue(child, out var instanceId))
                        {
                            _bw.Write(instanceId.Index);
                        }
                        else
                        {
                            _bw.Align(field.Align);
                            Write(child);
                        }
                    }
                }
            }
            else if (node is RszStringNode stringNode)
            {
                _bw.Align(4);
                _bw.Write(stringNode.Value.Length + 1);
                foreach (var ch in stringNode.Value)
                {
                    _bw.Write((short)ch);
                }
                _bw.Write((short)0);
            }
            else if (node is RszDataNode dataNode)
            {
                _bw.Write(dataNode.Data.Span);
            }
        }
    }
}
