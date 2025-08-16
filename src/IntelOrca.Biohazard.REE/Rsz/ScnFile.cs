using System;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.REE.Extensions;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public sealed class ScnFile(int version, ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x004E4353;

        public ReadOnlyMemory<byte> Data => data;

        public int Version => version;
        private ScnHeader Header => new ScnHeader(Version, version <= 18 ? data[..56] : data[..64]);
        private ReadOnlySpan<UserDataInfo> UserDataInfoList => data.Get<UserDataInfo>(Header.UserdataOffset, Header.UserDataCount);
        private ReadOnlySpan<FolderInfo> FolderInfoList => data.Get<FolderInfo>(Header.FolderOffset, Header.FolderCount);
        private RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

        public Builder ToBuilder(RszTypeRepository repository)
        {
            var objectList = Rsz.ReadObjectList(repository);
            return new Builder(this);
        }

        public class Builder
        {
            public int Version { get; }

            public Builder(ScnFile instance)
            {
                Version = instance.Version;
            }

            public ScnFile Build()
            {
                return new ScnFile(Version, new byte[0]);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FolderInfo
        {
            public int ObjectId;
            public int ParentId;
        }
    }
}
