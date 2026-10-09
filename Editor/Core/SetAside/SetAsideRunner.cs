using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>Why set-aside changes cannot be brought back right now.</summary>
    public enum SetAsideBlock
    {
        None,

        /// <summary>The working tree has changes; they must be saved first so nothing is mixed up.</summary>
        WorkingTreeHasChanges,

        /// <summary>A file changed both in the set-aside changes and in the history since; git would have to merge it.</summary>
        Overlap,
    }

    public sealed class SetAsidePlan
    {
        public SetAsideBlock Block { get; }

        /// <summary>Files the set-aside changes touch (tracked changes and files that were new).</summary>
        public IReadOnlyList<string> Paths { get; }

        /// <summary>For <see cref="SetAsideBlock.Overlap"/>: the files changed on both sides.</summary>
        public IReadOnlyList<string> OverlappingPaths { get; }

        public bool CanApply => Block == SetAsideBlock.None;

        public SetAsidePlan(SetAsideBlock block, IReadOnlyList<string> paths, IReadOnlyList<string> overlappingPaths)
        {
            Block = block;
            Paths = paths ?? Array.Empty<string>();
            OverlappingPaths = overlappingPaths ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Brings set-aside changes back (「取り出す」) without ever producing a conflict: git stash apply is
    /// a three-way merge, so it only runs when the working tree is clean and no file was changed both in
    /// the stash and in the history since the stash was made. Otherwise nothing is touched and the plan
    /// says why. The stash itself is kept, so the user can always try again.
    /// </summary>
    internal static class SetAsideRunner
    {
        public static async Task<SetAsidePlan> PlanAsync(IGitRepository repository, StashEntry stash, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (stash == null) throw new ArgumentNullException(nameof(stash));

            var hash = stash.Hash;
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in await repository.GetChangedPathsAsync(hash + "^1", hash, cancellationToken).ConfigureAwait(false)) paths.Add(path);
            // Files that were new (untracked) when the stash was made live in its third parent.
            if (await repository.RevisionExistsAsync(hash + "^3", cancellationToken).ConfigureAwait(false))
            {
                foreach (var path in await repository.GetTreePathsAsync(hash + "^3", cancellationToken).ConfigureAwait(false)) paths.Add(path);
            }
            var pathList = new List<string>(paths);

            var status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.HasChanges) return new SetAsidePlan(SetAsideBlock.WorkingTreeHasChanges, pathList, null);

            var since = await repository.GetChangedPathsAsync(hash + "^1", "HEAD", cancellationToken).ConfigureAwait(false);
            var overlap = new List<string>();
            foreach (var path in since)
            {
                if (paths.Contains(path)) overlap.Add(path);
            }
            overlap.Sort(StringComparer.Ordinal);
            return overlap.Count > 0
                ? new SetAsidePlan(SetAsideBlock.Overlap, pathList, overlap)
                : new SetAsidePlan(SetAsideBlock.None, pathList, null);
        }

        /// <summary>
        /// Applies the stash when <see cref="PlanAsync"/> allows it and returns the plan it acted on.
        /// The caller has already asked the user to confirm.
        /// </summary>
        public static async Task<SetAsidePlan> ApplyAsync(IGitRepository repository, StashEntry stash, CancellationToken cancellationToken)
        {
            var plan = await PlanAsync(repository, stash, cancellationToken).ConfigureAwait(false);
            if (!plan.CanApply) return plan;
            await repository.StashApplyAsync(stash.Hash, cancellationToken).ConfigureAwait(false);
            return plan;
        }
    }
}
