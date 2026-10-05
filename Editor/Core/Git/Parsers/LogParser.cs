using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shiori
{
    /// <summary>
    /// Parses <c>git log -z --format=%x1e%H%x00%at%x00%s%x00%an --name-status</c>.
    /// Each commit starts with a record separator (0x1E) prefixed hash; the header fields are
    /// NUL-separated; the first name-status token after the header carries a leading newline.
    /// </summary>
    internal static class LogParser
    {
        public const string Format = "%x1e%H%x00%at%x00%s%x00%an";
        private const char RecordStart = '\x1e';

        public static IReadOnlyList<Snapshot> Parse(string stdout)
        {
            var tokens = Tokenizer.SplitNul(stdout);
            var snapshots = new List<Snapshot>();
            var i = 0;
            while (i < tokens.Count)
            {
                var token = tokens[i];
                if (token.Length == 0 || token[0] != RecordStart)
                {
                    // Stray token (should not happen); skip to the next record.
                    i++;
                    continue;
                }
                if (i + 3 >= tokens.Count) break;

                var hash = token.Substring(1);
                var time = ParseUnixSeconds(tokens[i + 1]);
                var subject = tokens[i + 2];
                var author = tokens[i + 3];
                i += 4;

                // The first file token begins with '\n' (from "%an\0\n"); strip it so it reads as a status.
                if (i < tokens.Count && tokens[i].Length > 0 && tokens[i][0] == '\n')
                {
                    tokens = ReplaceAt(tokens, i, tokens[i].Substring(1));
                }

                var changes = NameStatusParser.ParseTokens(tokens, ref i, t => t[0] == RecordStart);
                snapshots.Add(new Snapshot(hash, time, subject, author, changes));
            }
            return snapshots;
        }

        private static DateTimeOffset ParseUnixSeconds(string text)
        {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : DateTimeOffset.MinValue;
        }

        private static IReadOnlyList<string> ReplaceAt(IReadOnlyList<string> tokens, int index, string value)
        {
            if (tokens is List<string> list)
            {
                list[index] = value;
                return list;
            }
            var copy = new List<string>(tokens);
            copy[index] = value;
            return copy;
        }
    }
}
