using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;

namespace IntelOrca.Biohazard.REEUtils
{
    internal static class PakPathMatcher
    {
        public static string[] FindMatchingEntries(IPakFile pak, PakList pakList, IReadOnlyList<string> patterns)
        {
            var existingHashes = new HashSet<ulong>(pak.FileHashes);
            var results = new List<string>();

            foreach (var entry in pakList.Entries)
            {
                if (string.IsNullOrEmpty(entry))
                    continue;

                ulong hash;
                try
                {
                    hash = ComputeNormalizedPathHash(entry);
                }
                catch
                {
                    continue;
                }

                if (!existingHashes.Contains(hash))
                    continue;

                if (MatchesPatterns(entry, patterns))
                    results.Add(entry);
            }

            return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static bool MatchesPatterns(string entry, IReadOnlyList<string> patterns)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.Contains('*') || pattern.Contains('?'))
                {
                    var rx = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
                    if (rx.IsMatch(entry))
                        return true;
                }
                else if (entry.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static ulong ComputeNormalizedPathHash(string path)
        {
            path = path.Replace("\\", "/");
            if (path.Contains("__Unknown"))
            {
                var pathWithoutExtension = Path.GetFileNameWithoutExtension(path);
                return Convert.ToUInt64(pathWithoutExtension, 16);
            }

            var lower = (uint)MurMur3.HashData(path.ToLowerInvariant());
            var upper = (uint)MurMur3.HashData(path.ToUpperInvariant());
            return ((ulong)upper << 32) | lower;
        }
    }
}
