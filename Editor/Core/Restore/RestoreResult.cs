using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>What to do with uncommitted changes before restoring (F4 dialog choices).</summary>
    public enum RestoreMode
    {
        /// <summary>「保存してから戻す」: commit the working tree first, then restore.</summary>
        SaveFirst,

        /// <summary>「保存せずに戻す」: stash the working tree (never discard), then restore.</summary>
        StashFirst,
    }

    public sealed class RestoreResult
    {
        /// <summary>Hash of the "Restore: ..." commit, or null when the tree already matched the target.</summary>
        public string RestoreCommitHash { get; }

        /// <summary>Hash of the snapshot taken before restoring (SaveFirst only), or null.</summary>
        public string SavedCommitHash { get; }

        /// <summary>Hash of the stash holding the previous working tree (StashFirst only), or null when there was nothing to stash.</summary>
        public string StashHash { get; }

        /// <summary>Files the restore changed in the working tree.</summary>
        public IReadOnlyList<FileChange> Changes { get; }

        public bool ChangedAnything => RestoreCommitHash != null;

        public bool TouchedProjectSettings
        {
            get
            {
                foreach (var change in Changes)
                {
                    if (change.Path.StartsWith("ProjectSettings/", StringComparison.Ordinal)) return true;
                }
                return false;
            }
        }

        /// <summary>Repository-relative paths of scenes (.unity) the restore changed.</summary>
        public IReadOnlyList<string> ChangedScenePaths
        {
            get
            {
                var scenes = new List<string>();
                foreach (var change in Changes)
                {
                    if (change.Path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) scenes.Add(change.Path);
                }
                return scenes;
            }
        }

        public RestoreResult(string restoreCommitHash, string savedCommitHash, string stashHash, IReadOnlyList<FileChange> changes)
        {
            RestoreCommitHash = restoreCommitHash;
            SavedCommitHash = savedCommitHash;
            StashHash = stashHash;
            Changes = changes ?? Array.Empty<FileChange>();
        }
    }
}
