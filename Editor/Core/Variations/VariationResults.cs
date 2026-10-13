using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>What to do with unsaved changes when switching variations (the switch dialog's choices).</summary>
    public enum SwitchMode
    {
        /// <summary>「保存してから切り替える」: save them into the current variation first.</summary>
        SaveFirst,

        /// <summary>「変更を持っていく」: keep them in the working tree, so they move to the other variation.</summary>
        Carry,
    }

    public enum CreateOutcome
    {
        Created,

        /// <summary>Nothing usable is left of the name, or git does not accept it.</summary>
        InvalidName,

        /// <summary>A branch with that name already exists here or at the 送信先.</summary>
        AlreadyExists,
    }

    public enum SwitchOutcome
    {
        Switched,

        /// <summary>Carried changes would be overwritten by the other variation's files; nothing changed.</summary>
        Blocked,
    }

    public sealed class SwitchResult
    {
        public SwitchOutcome Outcome { get; }

        /// <summary>The save made before switching (<see cref="SwitchMode.SaveFirst"/> with changes), or null.</summary>
        public string SavedCommitHash { get; }

        /// <summary>Paths whose content differs between the old and the new variation, with '/'.</summary>
        public IReadOnlyList<string> ChangedPaths { get; }

        /// <summary>For <see cref="SwitchOutcome.Blocked"/>: the unsaved files that stood in the way.</summary>
        public IReadOnlyList<string> BlockingPaths { get; }

        public SwitchResult(SwitchOutcome outcome, string savedCommitHash, IReadOnlyList<string> changedPaths, IReadOnlyList<string> blockingPaths)
        {
            Outcome = outcome;
            SavedCommitHash = savedCommitHash;
            ChangedPaths = changedPaths ?? Array.Empty<string>();
            BlockingPaths = blockingPaths ?? Array.Empty<string>();
        }

        public bool TouchedProjectSettings => ChangedPathsUnder.ProjectSettings(ChangedPaths);

        public IReadOnlyList<string> ChangedScenePaths => ChangedPathsUnder.Scenes(ChangedPaths);
    }

    public enum MergeOutcome
    {
        Merged,

        /// <summary>The current branch already has everything; nothing changed.</summary>
        UpToDate,

        /// <summary>The working tree has changes; merging was not started.</summary>
        Unsaved,

        /// <summary>Git stopped on conflicts; the merge was aborted and nothing changed.</summary>
        Conflicted,
    }

    public sealed class MergeResult
    {
        public MergeOutcome Outcome { get; }

        /// <summary>For <see cref="MergeOutcome.Merged"/>: paths the merge changed in the working tree.</summary>
        public IReadOnlyList<string> ChangedPaths { get; }

        /// <summary>For <see cref="MergeOutcome.Conflicted"/>: paths git could not merge.</summary>
        public IReadOnlyList<string> ConflictPaths { get; }

        public MergeResult(MergeOutcome outcome, IReadOnlyList<string> changedPaths, IReadOnlyList<string> conflictPaths)
        {
            Outcome = outcome;
            ChangedPaths = changedPaths ?? Array.Empty<string>();
            ConflictPaths = conflictPaths ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> ChangedScenePaths => ChangedPathsUnder.Scenes(ChangedPaths);
    }

    internal static class ChangedPathsUnder
    {
        public static bool ProjectSettings(IReadOnlyList<string> paths)
        {
            foreach (var path in paths)
            {
                if (path.StartsWith("ProjectSettings/", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static IReadOnlyList<string> Scenes(IReadOnlyList<string> paths)
        {
            var scenes = new List<string>();
            foreach (var path in paths)
            {
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) scenes.Add(path);
            }
            return scenes;
        }
    }
}
