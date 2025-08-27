using System;
using System.IO;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal class RszDataWriter
    {
        private readonly Stream _stream;
        private readonly Func<IRszNode, RszInstanceId> _getInstance;
        private readonly BinaryWriter _bw;
        private readonly long _baseAddress;

        public int BytesWritten => (int)(_stream.Position - _baseAddress);

        public RszDataWriter(Stream stream, Func<IRszNode, RszInstanceId> getInstance)
        {
            _stream = stream;
            _getInstance = getInstance;
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
                _bw.WriteZeros(align - rem);
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
                            WriteField(field, arrayNode.Children[j]);
                        }
                    }
                    else
                    {
                        Align(field.Align);
                        WriteField(field, structNode.Children[i]);
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

        public void WriteField(RszTypeField field, IRszNode node)
        {
            if (field.Type == RszFieldType.Object || field.Type == RszFieldType.UserData)
            {
                var instanceId = node is RszNullNode ? default : _getInstance(node);
                _bw.Write(instanceId.Index);
            }
            else if (field.Type == RszFieldType.String || field.Type == RszFieldType.Resource)
            {
                Write(node);
            }
            else
            {
                var oldPosition = _bw.BaseStream.Position;
                Write(node);
                var newPosition = _bw.BaseStream.Position;
                var bytesWritten = (int)(newPosition - oldPosition);
                if (bytesWritten < field.Size)
                {
                    _bw.WriteZeros(field.Size - bytesWritten);
                }
                else if (bytesWritten > field.Size)
                {
                    throw new Exception($"{field.Name} is {field.Size} bytes, but {bytesWritten} was written.");
                }
            }
        }
    }
}
