using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
            var instanceInfoList = InstanceInfoList;
            for (var i = 0; i < instanceInfoList.Length; i++)
            {
                result.Add(deserializer.Read(new RszInstanceId(i), instanceInfoList[i]));
            }

            // for (var i = 0; i < result.Count; i++)
            // {
            //     result[i] = VisitInstanceTree(result[i], result);
            // }

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
            else if (instance.Value is RszInstanceId[] references)
            {
                var dereferenced = new RszInstance[references.Length];
                for (var i = 0; i < references.Length; i++)
                {
                    dereferenced[i] = list[references[i].Index];
                }
                instance = new RszInstance(instance.Id, instance.Type, dereferenced);
            }
            return instance;
        }

        private ImmutableArray<RszInstance> GetObjectInstances(RszTypeRepository repository)
        {
            var instanceList = GetInstances(repository);
            var objectList = ImmutableArray.CreateBuilder<RszInstance>();
            foreach (var instanceId in ObjectInstanceIds)
            {
                objectList.Add(instanceList[instanceId]);
            }
            return objectList.ToImmutable();
        }

        private ImmutableArray<object> GetObjects(RszTypeRepository repository)
        {
            var clrDeserializer = new RszInstanceClrDeserializer();
            var clrInstances = clrDeserializer.Deserialize(GetInstances(repository));
            var objectInstanceIds = ObjectInstanceIds;
            var result = ImmutableArray.CreateBuilder<object>();
            for (var i = 0; i < objectInstanceIds.Length; i++)
            {
                result.Add(clrInstances[objectInstanceIds[i]]);
            }
            return result.ToImmutable();
        }

        public Builder ToBuilder(RszTypeRepository repository)
        {
            return new Builder(repository, this);
        }

        public class Builder
        {
            public RszTypeRepository Repository { get; }
            public List<object> Objects { get; }

            public Builder(RszTypeRepository repository, RszFile instance)
            {
                Repository = repository;
                Objects = instance.GetObjects(repository).ToList();
            }

            public RszFile Build()
            {
                return new RszFile(new byte[0]);
            }
        }
    }
}
