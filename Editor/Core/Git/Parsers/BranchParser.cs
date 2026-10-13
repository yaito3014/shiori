using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shiori
{
    /// <summary>
    /// Parses <c>git for-each-ref --format=&lt;Format&gt; refs/heads refs/remotes/origin</c>: one line per ref,
    /// fields separated by NUL. Ref names cannot hold control characters and subjects are one line.
    /// </summary>
    internal static class BranchParser
    {
        public const string Format = "%(refname)%00%(objectname)%00%(HEAD)%00%(committerdate:unix)%00%(contents:subject)";

        private const string LocalPrefix = "refs/heads/";
        private const string RemotePrefix = "refs/remotes/origin/";

        /// <summary>Local branches in git's order, then branches only <c>origin</c> has. <c>origin/HEAD</c> is skipped.</summary>
        public static IReadOnlyList<BranchInfo> Parse(string stdout)
        {
            var locals = new List<string[]>();
            var remotes = new List<string[]>();
            var onRemote = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in (stdout ?? string.Empty).Split('\n'))
            {
                var fields = line.TrimEnd('\r').Split('\0');
                if (fields.Length < 5) continue;
                if (fields[0].StartsWith(LocalPrefix, StringComparison.Ordinal))
                {
                    fields[0] = fields[0].Substring(LocalPrefix.Length);
                    locals.Add(fields);
                }
                else if (fields[0].StartsWith(RemotePrefix, StringComparison.Ordinal))
                {
                    fields[0] = fields[0].Substring(RemotePrefix.Length);
                    if (fields[0] == "HEAD") continue;
                    remotes.Add(fields);
                    onRemote.Add(fields[0]);
                }
            }

            var branches = new List<BranchInfo>();
            var localNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in locals)
            {
                localNames.Add(f[0]);
                branches.Add(new BranchInfo(f[0], f[1], f[2] == "*", false, onRemote.Contains(f[0]), ParseTime(f[3]), f[4]));
            }
            foreach (var f in remotes)
            {
                if (localNames.Contains(f[0])) continue;
                branches.Add(new BranchInfo(f[0], f[1], false, true, true, ParseTime(f[3]), f[4]));
            }
            return branches;
        }

        private static DateTimeOffset ParseTime(string text)
        {
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : DateTimeOffset.MinValue;
        }
    }
}
