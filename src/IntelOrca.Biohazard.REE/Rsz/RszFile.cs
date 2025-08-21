using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public class RszFile(ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x005A5352;

        public ReadOnlyMemory<byte> Data => data;

        private RszHeader Header => new RszHeader(data);
        private ReadOnlySpan<RszInstanceId> ObjectInstanceIds => data.Get<RszInstanceId>(Header.Size, (int)Header.ObjectCount);
        private ReadOnlySpan<RszInstanceInfo> InstanceInfoList => data.Get<RszInstanceInfo>(Header.InstanceOffset, Header.InstanceCount);
        internal ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserDataOffset, Header.UserDataCount);
        private ReadOnlySpan<byte> InstanceData => data.Slice((int)Header.DataOffset).Span;

        internal int Version => (int)Header.Version;

        internal ImmutableArray<string> UserDataInfoPaths
        {
            get
            {
                var result = ImmutableArray.CreateBuilder<string>();
                var userDataInfoList = UserDataInfoList;
                for (var i = 0; i < userDataInfoList.Length; i++)
                {
                    result.Add(GetString(userDataInfoList[i].PathOffset));
                }
                return result.ToImmutable();
            }
        }

        private string GetString(ulong offset)
        {
            if (offset != 0)
            {
                var span = MemoryMarshal.Cast<byte, char>(Data.Slice((int)offset).Span);
                for (var i = 0; i < span.Length; i++)
                {
                    if (span[i] == '\0')
                    {
                        return new string(span.Slice(0, i).ToArray());
                    }
                }
            }
            return string.Empty;
        }

        private ImmutableArray<RszInstance> ReadInstanceList(RszTypeRepository repository)
        {
            var instanceInfoList = InstanceInfoList;
            var instanceRszTypes = new RszType[instanceInfoList.Length];
            for (var i = 0; i < instanceRszTypes.Length; i++)
            {
                var rszTypeId = instanceInfoList[i].TypeId;
                instanceRszTypes[i] = repository.FromId(rszTypeId) ?? throw new Exception($"Type ID {rszTypeId} not found");
            }

            var userDataInfoList = UserDataInfoList;

            var result = ImmutableArray.CreateBuilder<RszInstance>();
            result.Count = instanceInfoList.Length;
            for (var i = 0; i < userDataInfoList.Length; i++)
            {
                var instanceIndex = userDataInfoList[i].InstanceId;
                var rszType = instanceRszTypes[instanceIndex];
                var path = GetString(userDataInfoList[i].PathOffset);
                result[instanceIndex] = new RszInstance(new RszInstanceId(instanceIndex), new RszUserDataNode(rszType, path));
            }

            var rszDataReader = new RszDataReader(repository, new SpanReader(InstanceData));
            for (var i = 0; i < instanceInfoList.Length; i++)
            {
                if (result[i].Id.Index != 0)
                    continue;

                var rszType = instanceRszTypes[i];
                var rszValue = rszType.Id == 0 ? new RszNullNode() : (IRszNode)rszDataReader.ReadStruct(rszType);
                result[i] = new RszInstance(new RszInstanceId(i), rszValue);
            }

            for (var i = 0; i < instanceInfoList.Length; i++)
            {
                VisitInstanceTree(result[i].Value, x =>
                {
                    if (x is RszDataNode dataNode)
                    {
                        if (dataNode.Type == RszFieldType.Object ||
                            dataNode.Type == RszFieldType.UserData)
                        {
                            var instanceId = dataNode.AsInt32();
                            return result[instanceId].Value;
                        }
                    }
                    return x;
                });
            }

            return result.ToImmutable();
        }

        private IRszNode VisitInstanceTree(IRszNode node, Func<IRszNode, IRszNode> transform)
        {
            if (node.Children.IsDefaultOrEmpty)
                return transform(node);

            var array = node.Children.ToBuilder();
            for (var i = 0; i < node.Children.Length; i++)
            {
                array[i] = VisitInstanceTree(array[i], transform);
            }
            node.Children = array.ToImmutable();
            return transform(node);
        }

        public ImmutableArray<IRszNode> ReadObjectList(RszTypeRepository repository)
        {
            var instanceList = ReadInstanceList(repository);
            var objectList = ImmutableArray.CreateBuilder<IRszNode>();
            foreach (var instanceId in ObjectInstanceIds)
            {
                objectList.Add(instanceList[instanceId.Index].Value);
            }
            return objectList.ToImmutable();
        }

        public Builder ToBuilder(RszTypeRepository repository)
        {
            return new Builder(repository, this);
        }

        public class Builder
        {
            public RszTypeRepository Repository { get; }
            public int Version { get; }
            public ImmutableArray<IRszNode> Objects { get; set; } = [];

            public Builder(RszTypeRepository repository, int version)
            {
                Repository = repository;
                Version = version;
            }

            public Builder(RszTypeRepository repository, RszFile instance)
            {
                Repository = repository;
                Version = (int)instance.Header.Version;
                Objects = instance.ReadObjectList(repository);
            }

            public RszFile Build()
            {
                var (instanceList, objectList) = GetInstances();
                var instanceMap = instanceList.ToDictionary(x => x.Value, x => x.Id);

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                var stringPool = new StringPoolBuilder(ms);

                // Reserve space for header
                bw.Skip(Version < 4 ? 32 : 48);

                // Object list
                foreach (var obj in objectList)
                {
                    bw.Write(obj.Id.Index);
                }

                // Instance list
                var instanceListOffset = ms.Position;
                foreach (var instance in instanceList)
                {
                    if (instance.Value is RszNullNode)
                    {
                        bw.Write(0);
                        bw.Write(0);
                    }
                    else if (instance.Value is RszUserDataNode userDataNode)
                    {
                        bw.Write(userDataNode.Type.Id);
                        bw.Write(userDataNode.Type.Crc);
                    }
                    else
                    {
                        var rszStruct = (RszStructNode)instance.Value!;
                        bw.Write(rszStruct.Type.Id);
                        bw.Write(rszStruct.Type.Crc);
                    }
                }

                bw.Align(16);
                var userDataOffset = ms.Position;
                var userDataCount = 0;
                for (var i = 0; i < instanceList.Length; i++)
                {
                    if (instanceList[i].Value is RszUserDataNode userDataNode)
                    {
                        bw.Write(i);
                        bw.Write(userDataNode.Type.Id);
                        stringPool.WriteStringOffset64(userDataNode.Path);
                        userDataCount++;
                    }
                }

                stringPool.WriteStrings();

                // Instance data
                bw.Align(16);
                var instanceDataOffset = ms.Position;
                var rszDataWriter = new RszDataWriter(ms, instanceMap);
                foreach (var instance in instanceList)
                {
                    rszDataWriter.Write(instance.Value!);
                }

                // Header
                ms.Position = 0;
                bw.Write(MAGIC);
                bw.Write(Version);
                bw.Write(Objects.Length);
                bw.Write(instanceList.Length);
                if (Version >= 4)
                {
                    bw.Write(userDataCount);
                    bw.Write(0); // Padding
                }
                bw.Write(instanceListOffset);
                bw.Write(instanceDataOffset);
                bw.Write(userDataOffset);

                return new RszFile(ms.ToArray());
            }

            private (ImmutableArray<RszInstance> Instances, ImmutableArray<RszInstance> Objects) GetInstances()
            {
                var instanceList = ImmutableArray.CreateBuilder<RszInstance>();
                var objectList = ImmutableArray.CreateBuilder<RszInstance>();

                // There is always a NULL instance at 0 (probably to prevent 0 from being used as a reference ID)
                instanceList.Add(new RszInstance(new RszInstanceId(0), new RszNullNode()));

                foreach (var obj in Objects)
                {
                    objectList.Add(CreateInstanceTree(obj, instanceList));
                }
                return (instanceList.ToImmutable(), objectList.ToImmutable());
            }

            private static RszInstance CreateInstanceTree(IRszNode node, ImmutableArray<RszInstance>.Builder builder)
            {
                if (node is RszStructNode structNode)
                {
                    AddInstances(structNode);
                    return AddInstance(structNode);
                }
                else
                {
                    throw new NotSupportedException("Non struct node added to object list.");
                }

                void AddInstances(RszStructNode node)
                {
                    var rszType = node.Type;
                    for (var i = 0; i < rszType.Fields.Length; i++)
                    {
                        var child = node.Children[i];
                        var rszField = rszType.Fields[i];
                        if (rszField.IsArray)
                        {
                            var childArray = (RszArrayNode)child;
                            for (var j = 0; j < childArray.Children.Length; j++)
                            {
                                if (childArray.Children[j] is RszStructNode childStructNode)
                                {
                                    AddInstances(childStructNode);
                                    AddInstance(childStructNode);
                                }
                                else if (childArray.Children[j] is RszUserDataNode userDataNode)
                                {
                                    AddInstance(userDataNode);
                                }
                            }
                        }
                        else
                        {
                            if (child is RszStructNode childStructNode)
                            {
                                AddInstances(childStructNode);
                                if (rszField.Type == RszFieldType.Object ||
                                    rszField.Type == RszFieldType.UserData)
                                {
                                    AddInstance(child);
                                }
                            }
                        }
                    }
                }

                RszInstance AddInstance(IRszNode node)
                {
                    if (node is RszNullNode)
                    {
                        return builder[0];
                    }
                    else
                    {
                        var instance = new RszInstance(new RszInstanceId(builder.Count), node);
                        builder.Add(instance);
                        return instance;
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RszInstanceInfo
        {
            public uint TypeId;
            public uint Crc;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct UserDataInfo
        {
            public int InstanceId;
            public uint TypeId;
            public ulong PathOffset;
        }
    }
}
