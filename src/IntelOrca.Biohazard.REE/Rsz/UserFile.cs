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

        public ImmutableArray<IRszNode> GetObjects(RszTypeRepository repository) => Rsz.ReadObjectList(repository);

        public Builder ToBuilder(RszTypeRepository repository)
        {
            return new Builder(repository, this);
        }

        public class Builder
        {
            public RszTypeRepository Repository { get; }
            public int RszVersion { get; }
            public ImmutableArray<IRszNode> Objects { get; set; }

            public Builder(RszTypeRepository repository, UserFile instance)
            {
                Repository = repository;
                RszVersion = instance.Rsz.Version;
                Objects = instance.Rsz.ReadObjectList(repository);
            }

            public UserFile Build()
            {
                var rszBuilder = new RszFile.Builder(Repository, RszVersion);
                rszBuilder.Objects = Objects;
                var rsz = rszBuilder.Build();

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                var stringPool = new StringPoolBuilder(ms);

                // Reserve space for header
                bw.Skip(48);

                // Resources
                bw.Align(16);
                var resourceOffset = ms.Position;

                // Userdata
                bw.Align(16);
                var userDataOffset = 0L;
                var userDataCount = 0;
                userDataOffset = ms.Position;
                var userDataList = rsz.UserDataInfoList;
                var userDataListPaths = rsz.UserDataInfoPaths;
                for (var i = 0; i < userDataList.Length; i++)
                {
                    bw.Write(userDataList[i].TypeId);
                    bw.Write(0);
                    stringPool.WriteStringOffset64(userDataListPaths[i]);
                }
                userDataCount = userDataList.Length;

                // Strings
                bw.Align(16);
                stringPool.WriteStrings();

                // Instance data
                var rszDataOffset = ms.Position;
                rszBuilder.AlignOffset = rszDataOffset;
                rsz = rszBuilder.Build();
                bw.Write(rsz.Data.Span);

                // Header
                ms.Position = 0;
                bw.Write(MAGIC);
                bw.Write(0); // Resource count
                bw.Write(userDataCount); // User data count
                bw.Write(0); // Info count
                bw.Write(resourceOffset); // Resource offset
                bw.Write(userDataOffset); // User data offset
                bw.Write(rszDataOffset); // Data offset
                bw.Write(0UL); // Reserved

                return new UserFile(ms.ToArray());
            }
        }
    }
}
