using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    internal class RszFolderInfo
    {
        public const int Size = 8;

        public int Id { get; set; }
        public int ParentId { get; set; }

        public RszFolderInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < Size)
                throw new ArgumentException("Insufficient data for RszFolderInfo.");

            Id = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
            ParentId = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
        }
    }
}
