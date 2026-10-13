using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Parses <c>git ls-files -u -z</c>: one <c>&lt;mode&gt; &lt;hash&gt; &lt;stage&gt;\t&lt;path&gt;</c> per stage, grouped by path.</summary>
    internal static class ConflictParser
    {
        public static IReadOnlyList<ConflictEntry> Parse(string stdout)
        {
            var order = new List<string>();
            var stages = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var token in Tokenizer.SplitNul(stdout))
            {
                var tab = token.IndexOf('\t');
                if (tab <= 0) continue;
                var fields = token.Substring(0, tab).Split(' ');
                if (fields.Length != 3 || !int.TryParse(fields[2], out var stage) || stage < 1 || stage > 3) continue;
                var path = token.Substring(tab + 1);
                if (!stages.TryGetValue(path, out var hashes))
                {
                    hashes = new string[4];
                    stages[path] = hashes;
                    order.Add(path);
                }
                hashes[stage] = fields[1];
            }

            var entries = new List<ConflictEntry>(order.Count);
            foreach (var path in order)
            {
                var hashes = stages[path];
                entries.Add(new ConflictEntry(path, hashes[1], hashes[2], hashes[3]));
            }
            return entries;
        }
    }
}
