using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;
using ModelContextProtocol;

namespace IntelOrca.Biohazard.REEUtils
{
    internal static class McpEmbeddedData
    {
        private const string ResourcePrefix = "IntelOrca.Biohazard.REEUtils.data.";
        private static readonly Lazy<ImmutableArray<string>> s_supportedGames = new Lazy<ImmutableArray<string>>(GetSupportedGamesCore);

        public static string GetEngineDetailsMarkdown()
        {
            var data = EmbeddedData.GetFile("ree-engine-details.md");
            if (data == null)
                throw new McpException("Embedded RE Engine details markdown was not found.");

            return Encoding.UTF8.GetString(data);
        }

        public static ImmutableArray<string> GetSupportedGames() => s_supportedGames.Value;

        public static PakList GetPakList(string game)
        {
            var pakList = EmbeddedData.GetPakList(game);
            if (pakList == null)
                throw new McpException($"Embedded pak list for '{game}' was not found.");

            return pakList;
        }

        public static RszTypeRepository GetRszTypeRepository(string game)
        {
            var jsonGz = EmbeddedData.GetFile($"rsz{game}.json.gz");
            if (jsonGz != null)
                return RszRepositorySerializer.Default.FromJsonGz(jsonGz);

            var json = EmbeddedData.GetCompressedFile($"rsz{game}.json");
            if (json != null)
                return RszRepositorySerializer.Default.FromJson(json);

            throw new McpException($"Embedded RSZ repository for '{game}' was not found.");
        }

        public static RszTypeRepository LoadRszTypeRepositoryFromPath(string path)
        {
            EnsureFileExists(path, "RSZ repository");

            if (path.EndsWith(".json.gz", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                return RszRepositorySerializer.Default.FromJsonGz(File.ReadAllBytes(path));
            }

            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new McpException("RSZ repository path must end with .json or .json.gz.");

            return RszRepositorySerializer.Default.FromJsonFile(path);
        }

        public static PakList LoadPakListFromPath(string path)
        {
            EnsureFileExists(path, "Pak list");
            return PakList.FromFile(path);
        }

        private static void EnsureFileExists(string path, string kind)
        {
            if (!File.Exists(path))
                throw new McpException($"{kind} file '{path}' was not found.");
        }

        private static ImmutableArray<string> GetSupportedGamesCore()
        {
            var resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();

            var pakGames = resourceNames
                .Select(name => TryExtractGame(name, "paklist.", ".txt.gz"))
                .Where(name => name != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rszGames = resourceNames
                .Select(name => TryExtractGame(name, "rsz", ".json.gz"))
                .Where(name => name != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return [.. pakGames
                .Intersect(rszGames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
        }

        private static string? TryExtractGame(string resourceName, string prefix, string suffix)
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                return null;

            var shortName = resourceName.Substring(ResourcePrefix.Length);
            if (!shortName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !shortName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return shortName.Substring(prefix.Length, shortName.Length - prefix.Length - suffix.Length);
        }
    }
}
