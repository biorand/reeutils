using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IntelOrca.Biohazard.REE.Graphics;
using IntelOrca.Biohazard.REEUtils.GDeflate;

namespace IntelOrca.Biohazard.REEUtils.FileTypes
{
    internal sealed class TextureFileHandler(string path, byte[] data) : FileHandlerBase(path, data)
    {
        private static readonly IGDeflateCodec GDeflateCodec = ReeUtilsGDeflateCodec.Instance;

        public override byte[] Export()
        {
            return DdsFile.FromTexture(new TextureFile(Data), GDeflateCodec).ToBytes();
        }

        public override byte[] Import(string inputPath)
        {
            var dds = DdsFile.Read(File.ReadAllBytes(inputPath));
            return dds.ToTextureBytes(GetVersionOrDefault(GetFileInfo(Path), 36), GDeflateCodec);
        }

        public override Dictionary<string, object?> GetSummary()
        {
            var file = new TextureFile(Data);
            var summary = CreateSummary("TEX");
            summary["Version"] = file.RawVersion == file.EffectiveVersion
                ? file.RawVersion
                : $"{file.RawVersion} (effective {file.EffectiveVersion})";
            summary["Dimensions"] = $"{file.Width}x{file.Height}";
            summary["Compression"] = file.Compression;
            summary["Images"] = file.ImageCount;
            summary["Mips per image"] = file.MipCount;
            summary["Total mips"] = file.TotalMipCount;
            return summary;
        }

        public override JsonDocument GetJson(TreeOptions options)
        {
            throw new System.NotSupportedException("Texture files do not support JSON export.");
        }

        public override byte[] Import(JsonDocument json)
        {
            throw new System.NotSupportedException("Texture files do not support import.");
        }

        private static int GetVersionOrDefault((string Extension, int Version) info, int defaultVersion)
        {
            return info.Version == 0 ? defaultVersion : info.Version;
        }

        private static (string Extension, int Version) GetFileInfo(string path)
        {
            var extension = System.IO.Path.GetExtension(path);
            if (extension.Length > 1 && int.TryParse(extension[1..], out var version))
            {
                return (System.IO.Path.GetExtension(System.IO.Path.GetFileNameWithoutExtension(path)).ToLowerInvariant(), version);
            }

            return (extension.ToLowerInvariant(), 0);
        }

    }
}
