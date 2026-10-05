using System.Collections.Generic;

namespace Shiori
{
    /// <summary>
    /// Parses NUL-separated <c>--name-status -z</c> records: a status token
    /// (<c>M</c>, <c>A</c>, <c>D</c>, <c>T</c>, <c>R100</c>, <c>C075</c>, ...) followed by one path,
    /// or two paths for renames and copies (old first, then new).
    /// </summary>
    internal static class NameStatusParser
    {
        public static IReadOnlyList<FileChange> Parse(string stdout)
        {
            var tokens = Tokenizer.SplitNul(stdout);
            var index = 0;
            return ParseTokens(tokens, ref index, null);
        }

        /// <summary>
        /// Consumes status/path pairs from <paramref name="tokens"/> starting at <paramref name="index"/>
        /// until the tokens run out or <paramref name="stopWhen"/> returns true for a status token.
        /// </summary>
        public static List<FileChange> ParseTokens(IReadOnlyList<string> tokens, ref int index, System.Func<string, bool> stopWhen)
        {
            var changes = new List<FileChange>();
            while (index < tokens.Count)
            {
                var status = tokens[index];
                if (status.Length == 0)
                {
                    index++;
                    continue;
                }
                if (stopWhen != null && stopWhen(status)) break;

                var letter = status[0];
                if (letter == 'R' || letter == 'C')
                {
                    if (index + 2 >= tokens.Count) break;
                    var oldPath = tokens[index + 1];
                    var newPath = tokens[index + 2];
                    changes.Add(new FileChange(newPath, letter == 'R' ? ChangeKind.Renamed : ChangeKind.Copied, letter, '.', oldPath));
                    index += 3;
                }
                else
                {
                    if (index + 1 >= tokens.Count) break;
                    var path = tokens[index + 1];
                    changes.Add(new FileChange(path, KindFromLetter(letter), letter, '.'));
                    index += 2;
                }
            }
            return changes;
        }

        internal static ChangeKind KindFromLetter(char letter)
        {
            switch (letter)
            {
                case 'A': return ChangeKind.Added;
                case 'M': return ChangeKind.Modified;
                case 'D': return ChangeKind.Deleted;
                case 'R': return ChangeKind.Renamed;
                case 'C': return ChangeKind.Copied;
                case 'T': return ChangeKind.TypeChanged;
                case 'U': return ChangeKind.Unmerged;
                default: return ChangeKind.Unknown;
            }
        }
    }
}
