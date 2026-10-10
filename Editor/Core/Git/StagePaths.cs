using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>
    /// Which paths to stage or unstage when the user picks some changes in detail mode. Unity breaks
    /// when an asset and its .meta are committed apart, so the partner travels along when it is in the
    /// same list; a new folder's .meta comes along when a file inside it is staged; unstaging a rename
    /// brings its old path back too, so the index never keeps half a rename.
    /// </summary>
    internal static class StagePaths
    {
        private const string MetaSuffix = ".meta";

        /// <param name="chosen">The changes the user picked.</param>
        /// <param name="list">Every change in the list they were picked from (unstaged when staging, staged when unstaging).</param>
        /// <param name="staging">True to stage, false to unstage.</param>
        public static List<string> For(IEnumerable<FileChange> chosen, IReadOnlyList<FileChange> list, bool staging)
        {
            if (chosen == null) throw new ArgumentNullException(nameof(chosen));
            if (list == null) throw new ArgumentNullException(nameof(list));

            var byPath = new Dictionary<string, FileChange>(StringComparer.Ordinal);
            foreach (var change in list) byPath[change.Path] = change;

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Add(FileChange change)
            {
                if (!seen.Add(change.Path)) return;
                result.Add(change.Path);
                // The old path of a staged rename is gone from the working tree; only unstaging needs it.
                if (!staging && change.OldPath != null && seen.Add(change.OldPath)) result.Add(change.OldPath);
            }

            foreach (var change in chosen)
            {
                if (change == null) continue;
                Add(change);
                var partner = change.IsMeta ? change.Path.Substring(0, change.Path.Length - MetaSuffix.Length) : change.Path + MetaSuffix;
                if (byPath.TryGetValue(partner, out var pair)) Add(pair);
                if (!staging) continue;

                // A new folder's .meta: staging Assets/New/a.png without Assets/New.meta leaves Unity a folder without a GUID.
                var path = change.Path;
                for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
                {
                    if (byPath.TryGetValue(path.Substring(0, slash) + MetaSuffix, out var folder) && IsNew(folder)) Add(folder);
                }
            }
            return result;
        }

        private static bool IsNew(FileChange change)
        {
            return change.Kind == ChangeKind.Untracked || change.Kind == ChangeKind.Added;
        }
    }
}
