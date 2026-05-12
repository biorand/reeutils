using System;
using IntelOrca.Biohazard.REE.Rsz;
using IntelOrca.Biohazard.REEUtils.FileTypes;

namespace IntelOrca.Biohazard.REEUtils
{
    internal sealed class FileHandlerFactory
    {
        private const string NativesPrefix = "natives/stm/";

        public static FileHandlerFactory Default { get; } = new FileHandlerFactory();

        private FileHandlerFactory()
        {
        }

        public IFileHandler Create(string path, byte[] data, RszTypeRepository? repository = null)
        {
            var info = GetFileInfo(path);
            return info.Extension switch
            {
                ".msg" => new MessageFileHandler(path, data),
                ".fsm" => new HfsmFileHandler(path, data),
                ".user" => new UserFileHandler(path, data, GetVersionOrDefault(info, 2), repository),
                ".scn" => new SceneFileHandler(path, data, GetVersionOrDefault(info, 20), repository),
                ".pfb" => new PrefabFileHandler(path, data, GetVersionOrDefault(info, 17), repository),
                _ => throw new NotSupportedException($"Unsupported file format '{info.Extension}'.")
            };
        }

        public string GetReferencePath(string path)
        {
            if (path.StartsWith("natives/stm", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(12);

            var extensionIndex = path.LastIndexOf('.');
            if (extensionIndex != -1)
                path = path.Substring(0, extensionIndex);

            return path;
        }

        public string GetFullPathFromArg(string path)
        {
            if (path.StartsWith(NativesPrefix, StringComparison.OrdinalIgnoreCase))
                return path;

            var info = GetFileInfo(path);
            var suffix = info.Version != 0
                ? ""
                : info.Extension switch
                {
                    ".user" => ".2",
                    ".scn" => ".20",
                    ".pfb" => ".17",
                    _ => ""
                };
            return NativesPrefix + path + suffix;
        }

        private static int GetVersionOrDefault(FileInfo info, int defaultVersion)
        {
            return info.Version == 0 ? defaultVersion : info.Version;
        }

        private static FileInfo GetFileInfo(string path)
        {
            var extension = System.IO.Path.GetExtension(path);
            if (extension.Length > 1 && int.TryParse(extension.AsSpan(1), out var version))
            {
                return new FileInfo(System.IO.Path.GetExtension(System.IO.Path.GetFileNameWithoutExtension(path)).ToLowerInvariant(), version);
            }

            return new FileInfo(System.IO.Path.GetExtension(path).ToLowerInvariant(), 0);
        }

        private readonly record struct FileInfo(string Extension, int Version);
    }
}
