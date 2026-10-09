using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shiori
{
    /// <summary>Parses <c>git stash list -z --format=%gd%x00%H%x00%gs%x00%ct</c>.</summary>
    internal static class StashListParser
    {
        public const string Format = "%gd%x00%H%x00%gs%x00%ct";

        public static IReadOnlyList<StashEntry> Parse(string stdout)
        {
            var tokens = Tokenizer.SplitNul(stdout);
            var entries = new List<StashEntry>();
            for (var i = 0; i + 3 < tokens.Count; i += 4)
            {
                var time = long.TryParse(tokens[i + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                    : DateTimeOffset.MinValue;
                entries.Add(new StashEntry(tokens[i], tokens[i + 1], tokens[i + 2], time));
            }
            return entries;
        }
    }
}
