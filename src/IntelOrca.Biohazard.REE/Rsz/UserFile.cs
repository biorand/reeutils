using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.REE.Rsz
{
    public unsafe class UserFile(ReadOnlyMemory<byte> data)
    {
        private const uint MAGIC = 0x00525355;

        public ReadOnlyMemory<byte> Data => data;

        private UserHeader Header => MemoryMarshal.Read<UserHeader>(data.Span);
        private RszFile Rsz => new RszFile(data.Slice((int)Header.DataOffset));

        public Builder ToBuilder(RszTypeRepository repository)
        {
            var rszBuilder = Rsz.ToBuilder(repository);
            return new Builder(this);
        }

        public class Builder
        {
            public Builder(UserFile instance)
            {
            }

            public UserFile Build()
            {
                return new UserFile(new byte[0]);
            }
        }
    }
}
