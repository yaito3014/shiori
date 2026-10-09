using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>
    /// What a 戻す is about to change, computed before the confirmation dialog: the target snapshot
    /// and every path whose content differs between the current snapshot (HEAD) and the target.
    /// Unsaved working-tree changes are not included; the dialog handles those separately.
    /// </summary>
    public sealed class RestorePreview
    {
        public Snapshot Target { get; }

        /// <summary>Repository-relative paths with '/', sorted ordinally.</summary>
        public IReadOnlyList<string> ChangedPaths { get; }

        public RestorePreview(Snapshot target, IReadOnlyList<string> changedPaths)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            var sorted = new List<string>(changedPaths ?? Array.Empty<string>());
            sorted.Sort(StringComparer.Ordinal);
            ChangedPaths = sorted;
        }

        public bool Changes(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.Replace('\\', '/');
            foreach (var changed in ChangedPaths)
            {
                if (string.Equals(changed, normalized, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
