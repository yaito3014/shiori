using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Classifies the lines of a unified diff for display. Does not interpret the content.</summary>
    public static class DiffParser
    {
        public static IReadOnlyList<DiffLine> Parse(string diffText)
        {
            var lines = new List<DiffLine>();
            if (string.IsNullOrEmpty(diffText)) return lines;

            var inHunk = false;
            var raw = diffText.Split('\n');
            var count = raw.Length;
            if (count > 0 && raw[count - 1].Length == 0) count--; // trailing newline

            for (var i = 0; i < count; i++)
            {
                var line = raw[i];
                if (line.Length > 0 && line[line.Length - 1] == '\r') line = line.Substring(0, line.Length - 1);

                if (line.StartsWith("diff ", StringComparison.Ordinal))
                {
                    inHunk = false;
                    lines.Add(new DiffLine(DiffLineKind.Header, line));
                    continue;
                }
                if (line.StartsWith("@@", StringComparison.Ordinal))
                {
                    inHunk = true;
                    lines.Add(new DiffLine(DiffLineKind.Hunk, line));
                    continue;
                }
                if (!inHunk)
                {
                    lines.Add(new DiffLine(DiffLineKind.Header, line));
                    continue;
                }

                var first = line.Length > 0 ? line[0] : ' ';
                switch (first)
                {
                    case '+': lines.Add(new DiffLine(DiffLineKind.Added, line)); break;
                    case '-': lines.Add(new DiffLine(DiffLineKind.Removed, line)); break;
                    case '\\': lines.Add(new DiffLine(DiffLineKind.Meta, line)); break;
                    default: lines.Add(new DiffLine(DiffLineKind.Context, line)); break;
                }
            }
            return lines;
        }
    }
}
