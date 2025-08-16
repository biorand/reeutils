using System;
using System.Collections.Immutable;
using System.IO;
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
        private ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserDataOffset, Header.UserDataCount);
        private ReadOnlySpan<byte> InstanceData => data.Slice((int)Header.DataOffset).Span;

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
                result[userDataInfoList[i].InstanceId] = new RszInstance()
                {
                    Id = new RszInstanceId(i),
                    Value = new RszUserDataNode(rszType)
                };
            }

            var rszDataReader = new RszDataReader(repository, new SpanReader(InstanceData));
            for (var i = 0; i < instanceInfoList.Length; i++)
            {
                if (result[i] != null)
                    continue;

                var rszType = instanceRszTypes[i];
                result[i] = new RszInstance()
                {
                    Id = new RszInstanceId(i),
                    Value = rszDataReader.ReadStruct(rszType)
                };
            }

            for (var i = 0; i < instanceInfoList.Length; i++)
            {
                VisitInstanceTree(result[i], x =>
                {
                    if (x is RszDataNode dataNode && dataNode.Type == RszFieldType.Object)
                    {
                        var instanceId = dataNode.AsInt32();
                        return result[instanceId];
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

        public ImmutableArray<RszInstance> ReadObjectList(RszTypeRepository repository)
        {
            var instanceList = ReadInstanceList(repository);
            var objectList = ImmutableArray.CreateBuilder<RszInstance>();
            foreach (var instanceId in ObjectInstanceIds)
            {
                objectList.Add(instanceList[instanceId.Index]);
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
            public uint Version { get; }
            public ImmutableArray<RszInstance> Objects { get; }

            public Builder(RszTypeRepository repository, RszFile instance)
            {
                Repository = repository;
                Version = instance.Header.Version;
                Objects = instance.ReadObjectList(repository);
            }

            public RszFile Build()
            {
                var instanceList = GetInstances();
                var objectList = Objects;

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

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
                    var rszStruct = (RszStructNode)instance.Value!;
                    bw.Write(rszStruct.Type.Id);
                    bw.Write(rszStruct.Type.Crc);
                }

                bw.Align(16);
                var userDataOffset = ms.Position;

                // Instance data
                bw.Align(16);
                var instanceDataOffset = ms.Position;
                var rszDataWriter = new RszDataWriter(ms);
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
                    bw.Write(0); // User data count
                    bw.Write(0); // Padding
                }
                bw.Write(instanceListOffset);
                bw.Write(instanceDataOffset);
                bw.Write(userDataOffset);

                return new RszFile(ms.ToArray());
            }

            private ImmutableArray<RszInstance> GetInstances()
            {
                var instanceList = ImmutableArray.CreateBuilder<RszInstance>();

                // There is always a NULL instance at 0 (probably to prevent 0 from being used as a reference ID)
                instanceList.Add(new RszInstance()
                {
                    Value = new RszStructNode(Repository.FromId(0)!, [])
                });
                foreach (var obj in Objects)
                {
                    AddInstances(obj, instanceList);
                }
                return instanceList.ToImmutableArray();
            }

            private void AddInstances(IRszNode node, ImmutableArray<RszInstance>.Builder builder)
            {
                foreach (var child in node.Children)
                {
                    AddInstances(child, builder);
                }
                if (node is RszInstance instance)
                {
                    instance.Id = new RszInstanceId(builder.Count);
                    builder.Add(instance);
                }
            }
        }
    }
}
