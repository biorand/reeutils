using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public unsafe class UserFile(ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x00525355;

        public ReadOnlyMemory<byte> Data => data;

        private UserHeader Header => MemoryMarshal.Read<UserHeader>(data.Span);
        private RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

        /// <summary>Absolute offset of the embedded RSZ stream (the wrapper's DataOffset field).</summary>
        public int RszDataOffset => (int)Header.DataOffset;

        /// <summary>Number of resource-table entries in the wrapper (paths before the RSZ stream).</summary>
        public int ResourceCount => (int)Header.ResourceCount;

        /// <summary>Wrapper bytes before the RSZ stream (header + resource table + resource strings +
        /// userdata table). Preserved verbatim on rebuild so resource-bearing files round-trip.</summary>
        public byte[] Prefix => data.Slice(0, RszDataOffset).ToArray();

        public int RszVersion => Rsz.Version;

        public int InstanceCount => Rsz.InstanceCount;

        public ImmutableArray<RszObjectNode> GetObjects(RszTypeRepository repository) => Rsz.ReadObjectList(repository);

        public Builder ToBuilder(RszTypeRepository repository)
        {
            return new Builder(repository, this);
        }

        public class Builder
        {
            public RszTypeRepository Repository { get; }
            public int RszVersion { get; }
            public ImmutableArray<RszObjectNode> Objects { get; set; }

            /// <summary>Wrapper bytes before the RSZ stream, preserved from the source file (the header,
            /// resource table, resource strings and userdata table). The RSZ stream is re-emitted below
            /// it, so resource-bearing .user files (e.g. oniws BankList/TriggerInfoList/EffectParam)
            /// round-trip without dropping their resource table.</summary>
            public byte[] PreservedPrefix { get; set; }

            public Builder(RszTypeRepository repository, UserFile instance)
            {
                Repository = repository;
                RszVersion = instance.Rsz.Version;
                Objects = instance.Rsz.ReadObjectList(repository);
                PreservedPrefix = instance.Prefix;
            }

            public UserFile Build()
            {
                var rszBuilder = new RszFile.Builder(Repository, RszVersion);
                rszBuilder.Objects = Objects;
                // Alignment inside the RSZ stream is computed against its absolute position in the
                // wrapper, which is the preserved prefix length (== the source DataOffset).
                rszBuilder.AlignOffset = PreservedPrefix.Length;
                var rsz = rszBuilder.Build();

                var ms = new MemoryStream();
                ms.Write(PreservedPrefix, 0, PreservedPrefix.Length);
                ms.Write(rsz.Data.Span);

                return new UserFile(ms.ToArray());
            }
        }
    }
}
