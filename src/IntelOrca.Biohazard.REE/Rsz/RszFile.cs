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
        private ReadOnlySpan<byte> InstanceData => data.Slice((int)Header.DataOffset).Span;

        private RszInstanceList ReadInstanceList(RszTypeRepository repository)
        {
            var rszDataReader = new RszDataReader(repository, new SpanReader(InstanceData));
            var result = ImmutableArray.CreateBuilder<RszInstance>();
            var instanceInfoList = InstanceInfoList;
            for (var i = 0; i < instanceInfoList.Length; i++)
            {
                result.Add(rszDataReader.Read(new RszInstanceId(i), instanceInfoList[i]));
            }
            return new RszInstanceList(result.ToImmutable());
        }

        public RszObjectList ReadObjectList(RszTypeRepository repository)
        {
            var instanceList = ReadInstanceList(repository);
            var objectList = ImmutableArray.CreateBuilder<RszInstance>();
            foreach (var instanceId in ObjectInstanceIds)
            {
                objectList.Add(instanceList.GetDeepHierarchy(instanceId));
            }
            return new RszObjectList(objectList.ToImmutable());
        }

        public Builder ToBuilder(RszTypeRepository repository)
        {
            return new Builder(repository, this);
        }

        public class Builder
        {
            public RszTypeRepository Repository { get; }
            public RszObjectList Objects { get; }

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
