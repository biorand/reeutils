using System;
using System.Buffers.Binary;

namespace IntelOrca.Biohazard.REE
{
    public static class FileVersion
    {
        public static int FromPath(string path)
        {
            var fullstopIndex = path.LastIndexOf('.');
            return int.TryParse(path.Substring(fullstopIndex + 1), out var version) ? version : -1;
        }

        public static FileKind DetectFileKind(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8)
                return FileKind.Unknown;

            return BinaryPrimitives.ReadUInt32LittleEndian(data) switch
            {
                0x00424650 => FileKind.Prefab,
                0x0046444D => FileKind.Material,
                0x004E4353 => FileKind.Scene,
                0x00525355 => FileKind.UserData,
                0x00584554 => FileKind.Texture,
                0x20534444 => FileKind.DDS,
                0x44484B42 => FileKind.BKHD,
                0x4853454D => FileKind.Mesh,
                0x4C4F4352 => FileKind.RCOL,
                _ => BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) switch
                {
                    0x47534D47 => FileKind.Message,
                    0x52495547 => FileKind.GUIR,
                    0x6B6E626D => FileKind.AudioBank,
                    0x74736C6D => FileKind.MLST,
                    _ => FileKind.Unknown
                },
            };
        }
    }

    public enum FileKind
    {
        Unknown,
        BKHD,
        DDS,
        GUIR,
        AudioBank,
        Material,
        Mesh,
        Message,
        MLST,
        Prefab,
        RCOL,
        Scene,
        Texture,
        UserData,
    }
}
