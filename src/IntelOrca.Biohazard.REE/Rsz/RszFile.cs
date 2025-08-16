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
        private ReadOnlySpan<int> ObjectInstanceIds => data.Get<int>(Header.Size, (int)Header.ObjectCount);
        private ReadOnlySpan<RszInstanceInfo> InstanceInfoList => data.Get<RszInstanceInfo>(Header.InstanceOffset, Header.InstanceCount);
        private ReadOnlySpan<byte> InstanceData => data.Slice((int)Header.DataOffset).Span;

        public ImmutableArray<RszInstance> GetInstances(RszTypeRepository repository)
        {
            var deserializer = new RszInstanceDeserializer(repository, InstanceData);
            var result = ImmutableArray.CreateBuilder<RszInstance>();
            foreach (var instanceInfo in InstanceInfoList)
            {
                result.Add(deserializer.Read(instanceInfo));
            }

            for (var i = 0; i < result.Count; i++)
            {
                result[i] = VisitInstanceTree(result[i], result);
            }

            return result.ToImmutable();
        }

        private static RszInstance VisitInstanceTree(RszInstance instance, ImmutableArray<RszInstance>.Builder list)
        {
            if (instance.Value is RszInstance[] children)
            {
                for (var i = 0; i < children.Length; i++)
                {
                    children[i] = VisitInstanceTree(children[i], list);
                }
            }
            else if (instance.Value is RszInstanceReference[] references)
            {
                var dereferenced = new RszInstance[references.Length];
                for (var i = 0; i < references.Length; i++)
                {
                    dereferenced[i] = list[references[i].Id];
                }
                instance = new RszInstance(instance.Type, dereferenced);
            }
            return instance;
        }

        public ImmutableArray<RszInstance> GetObjects(RszTypeRepository repository)
        {
            var instanceList = GetInstances(repository);
            var objectList = ImmutableArray.CreateBuilder<RszInstance>();
            foreach (var instanceId in ObjectInstanceIds)
            {
                objectList.Add(instanceList[instanceId]);
            }
            return objectList.ToImmutable();
        }

        public Builder ToBuilder()
        {
            return new Builder(this);
        }

        public class Builder
        {
            public Builder(RszFile instance)
            {
            }

            public RszFile Build()
            {
                return new RszFile(new byte[0]);
            }
        }
    }
}
