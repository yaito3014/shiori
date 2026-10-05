using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Parses <c>git stash list -z --format=%gd%x00%H%x00%gs</c>.</summary>
    internal static class StashListParser
    {
        public const string Format = "%gd%x00%H%x00%gs";

        public static IReadOnlyList<StashEntry> Parse(string stdout)
        {
            var tokens = Tokenizer.SplitNul(stdout);
            var entries = new List<StashEntry>();
            for (var i = 0; i + 2 < tokens.Count; i += 3)
            {
                entries.Add(new StashEntry(tokens[i], tokens[i + 1], tokens[i + 2]));
            }
            return entries;
        }
    }
}
