using System;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Messages
{
    public static class Language
    {
        private static readonly ImmutableDictionary<string, LanguageId> _map;

        static Language()
        {
            _map = Enumerable
                .Range(0, _isoCodes.Length)
                .ToImmutableDictionary(i => _isoCodes[i], x => (LanguageId)x);
        }

        public static string GetIsoNameFromId(LanguageId languageId)
        {
            if (languageId < 0 || (int)languageId >= _isoCodes.Length)
                throw new NotSupportedException("Language not supported.");
            return _isoCodes[(int)languageId];
        }

        public static LanguageId? GetIdFromIsoName(string name)
        {
            if (_map.TryGetValue(name, out var languageId))
                return languageId;

            var hyIndex = name.IndexOf('-');
            if (hyIndex == -1)
                return null;

            var baseName = name.Substring(0, hyIndex);
            if (_map.TryGetValue(baseName, out languageId))
                return languageId;

            return null;
        }

        private static readonly string[] _isoCodes = [
            "ja",
            "en",
            "fr",
            "it",
            "de",
            "es",
            "ru",
            "pl",
            "nl",
            "pt",
            "pt-BR",
            "ko",
            "zh-Hant",
            "zh-Hans",
            "fi",
            "sv",
            "da",
            "no",
            "cs",
            "hu",
            "sk",
            "ar",
            "tr",
            "bg",
            "el",
            "ro",
            "th",
            "uk",
            "vi",
            "id",
            "hi",
            "es-419",
        ];
    }
}
