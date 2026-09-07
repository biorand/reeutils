using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IntelOrca.Biohazard.REE.Rsz;

namespace ReeCompare
{
    public sealed record GameEntry(string Id, string DisplayName);

    internal static class GameCatalog
    {
        public const string CustomId = "custom";

        public static readonly IReadOnlyList<GameEntry> Games = new List<GameEntry>
        {
            new("re2", "Resident Evil 2"),
            new("re3", "Resident Evil 3"),
            new("re4", "Resident Evil 4"),
            new("re7", "Resident Evil 7"),
            new("re8", "Resident Evil 8 Village"),
            new("re9", "Resident Evil 9 Requiem"),
            new("oniws", "Onimusha: Way of the Sword"),
        };

        public static string DisplayNameFor(string? gameId)
        {
            if (string.IsNullOrEmpty(gameId))
                return DisplayNameFor("re4");
            if (string.Equals(gameId, CustomId, StringComparison.OrdinalIgnoreCase))
                return "Custom...";
            var match = Games.FirstOrDefault(g =>
                string.Equals(g.Id, gameId, StringComparison.OrdinalIgnoreCase));
            return match?.DisplayName ?? gameId!;
        }

        public static bool IsKnownGame(string? gameId) =>
            !string.IsNullOrEmpty(gameId) &&
            Games.Any(g => string.Equals(g.Id, gameId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Loads the embedded rsz{game}.json.gz linked into this assembly.
        /// Resource logical names vary with RootNamespace, so match by suffix.
        /// </summary>
        public static RszTypeRepository LoadEmbedded(string gameId)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var suffix = $"rsz{gameId.ToLowerInvariant()}.json.gz";
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
                throw new FileNotFoundException(
                    $"Embedded RSZ repository for '{gameId}' was not found (expected *{suffix}).");

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' could not be opened.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return RszRepositorySerializer.Default.FromJsonGz(ms.ToArray());
        }
    }
}
