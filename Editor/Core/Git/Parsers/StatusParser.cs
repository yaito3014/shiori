using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Parses <c>git status --porcelain=v2 -z --branch</c>.</summary>
    internal static class StatusParser
    {
        public static WorktreeStatus Parse(string stdout)
        {
            var tokens = Tokenizer.SplitNul(stdout);
            string branch = null;
            string head = null;
            var detached = false;
            var changes = new List<FileChange>();

            var i = 0;
            while (i < tokens.Count)
            {
                var token = tokens[i];
                i++;
                if (token.Length == 0) continue;

                switch (token[0])
                {
                    case '#':
                        if (token.StartsWith("# branch.oid "))
                        {
                            var oid = token.Substring("# branch.oid ".Length);
                            head = oid == "(initial)" ? null : oid;
                        }
                        else if (token.StartsWith("# branch.head "))
                        {
                            var name = token.Substring("# branch.head ".Length);
                            if (name == "(detached)")
                            {
                                detached = true;
                                branch = null;
                            }
                            else
                            {
                                branch = name;
                            }
                        }
                        break;

                    case '1':
                    {
                        // 1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>
                        var parts = token.Split(new[] { ' ' }, 9);
                        if (parts.Length < 9) break;
                        var x = parts[1][0];
                        var y = parts[1][1];
                        changes.Add(new FileChange(parts[8], Classify(x, y), x, y));
                        break;
                    }

                    case '2':
                    {
                        // 2 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <X><score> <path>  then NUL  <origPath>
                        var parts = token.Split(new[] { ' ' }, 10);
                        if (parts.Length < 10) break;
                        var x = parts[1][0];
                        var y = parts[1][1];
                        string origPath = null;
                        if (i < tokens.Count)
                        {
                            origPath = tokens[i];
                            i++;
                        }
                        var kind = parts[8][0] == 'C' ? ChangeKind.Copied : ChangeKind.Renamed;
                        changes.Add(new FileChange(parts[9], kind, x, y, origPath));
                        break;
                    }

                    case 'u':
                    {
                        // u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>
                        var parts = token.Split(new[] { ' ' }, 11);
                        if (parts.Length < 11) break;
                        changes.Add(new FileChange(parts[10], ChangeKind.Unmerged, parts[1][0], parts[1][1]));
                        break;
                    }

                    case '?':
                        if (token.Length > 2) changes.Add(new FileChange(token.Substring(2), ChangeKind.Untracked, '?', '?'));
                        break;

                    case '!':
                        // Ignored entries are only emitted with --ignored; we do not request them.
                        break;
                }
            }

            return new WorktreeStatus(branch, head, detached, changes);
        }

        /// <summary>Collapses the index/worktree pair into one kind, the way the simple mode presents it.</summary>
        internal static ChangeKind Classify(char x, char y)
        {
            if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D')) return ChangeKind.Unmerged;
            if (y == 'D' || x == 'D') return ChangeKind.Deleted;
            if (x == 'A') return ChangeKind.Added;
            if (x == 'R' || y == 'R') return ChangeKind.Renamed;
            if (x == 'C' || y == 'C') return ChangeKind.Copied;
            if (x == 'T' || y == 'T') return ChangeKind.TypeChanged;
            if (x == 'M' || y == 'M') return ChangeKind.Modified;
            return ChangeKind.Unknown;
        }
    }
}
