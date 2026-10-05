using System.Collections.Generic;

namespace Shiori.Editor
{
    internal enum UiLanguage
    {
        Japanese,
        English,
    }

    /// <summary>
    /// Looks up UI strings by key. M1 ships Japanese only; English returns the key itself
    /// so missing translations are visible rather than silently blank.
    /// </summary>
    internal static class L10n
    {
        public static UiLanguage Language { get; set; } = UiLanguage.Japanese;

        public static string Tr(string key)
        {
            if (Language == UiLanguage.Japanese && JapaneseStrings.Table.TryGetValue(key, out var text)) return text;
            return key;
        }

        public static string Tr(string key, params object[] args)
        {
            return string.Format(Tr(key), args);
        }

        internal static IReadOnlyDictionary<string, string> JapaneseTable => JapaneseStrings.Table;
    }
}
