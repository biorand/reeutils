using System;
using System.Collections.Immutable;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    internal class RszFile(ReadOnlyMemory<byte> data)
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
            public ImmutableArray<RszInstance> Objects { get; }

            public Builder(RszTypeRepository repository, RszFile instance)
            {
                Repository = repository;
                Objects = instance.ReadObjectList(repository);
            }

            public RszFile Build()
            {
                return new RszFile(new byte[0]);
            }
        }
    }
}
