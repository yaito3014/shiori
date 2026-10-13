using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>A merge in progress as the conflict panel shows it: whether there is one, and what is still undecided.</summary>
    public sealed class MergeState
    {
        public static readonly MergeState None = new MergeState(false, Array.Empty<ConflictEntry>());

        public bool InProgress { get; }

        /// <summary>Paths still waiting for 自分 / 相手, in index order.</summary>
        public IReadOnlyList<ConflictEntry> Conflicts { get; }

        /// <summary>Every conflict is decided, so the merge can be recorded.</summary>
        public bool ReadyToFinish => InProgress && Conflicts.Count == 0;

        public MergeState(bool inProgress, IReadOnlyList<ConflictEntry> conflicts)
        {
            InProgress = inProgress;
            Conflicts = conflicts ?? Array.Empty<ConflictEntry>();
        }
    }

    /// <summary>
    /// Merges that may stop on conflicts (M5). A merge starts only on a clean working tree; when it stops, every
    /// conflicted file gets this side's content in the working tree, so Unity never imports conflict markers or a
    /// half-merged scene, and the merge stays in progress until each file is decided or the merge is given up.
    /// Unity YAML files are merged object by object by UnityYAMLMerge when the merge driver is set up
    /// (see <see cref="MergeDriverSetup"/>); only what it cannot combine reaches the user.
    /// </summary>
    internal static class MergeRunner
    {
        /// <summary>
        /// Merges <paramref name="revision"/> into the current branch. Returns the conflicts it stopped on, or an empty
        /// list when the merge finished. The caller has made sure the working tree is clean.
        /// </summary>
        public static async Task<IReadOnlyList<ConflictEntry>> StartAsync(IGitRepository repository, string revision, string message, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var stopped = await repository.MergeAsync(revision, message, cancellationToken).ConfigureAwait(false);
            if (stopped.Count == 0) return Array.Empty<ConflictEntry>();

            var conflicts = await repository.GetConflictsAsync(cancellationToken).ConfigureAwait(false);
            // The files hold git's (or UnityYAMLMerge's) partial result now, never the user's own work: the tree was clean.
            var withOurs = new List<string>();
            foreach (var conflict in conflicts)
            {
                if (!conflict.IsDeletedOn(ConflictSide.Ours)) withOurs.Add(conflict.Path);
            }
            if (withOurs.Count > 0) await repository.CheckoutSideAsync(withOurs, ConflictSide.Ours, cancellationToken).ConfigureAwait(false);
            return conflicts;
        }

        public static async Task<MergeState> GetStateAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (!await repository.IsMergingAsync(cancellationToken).ConfigureAwait(false)) return MergeState.None;
            return new MergeState(true, await repository.GetConflictsAsync(cancellationToken).ConfigureAwait(false));
        }

        /// <summary>
        /// What a conflicted file holds while nothing is decided: this side's content, or the other side's where this side
        /// deleted it (git leaves that version in place). Null when neither side has the file.
        /// </summary>
        internal static string ShownHash(ConflictEntry entry) => entry.OursHash ?? entry.TheirsHash;

        /// <summary>
        /// Paths among <paramref name="entries"/> whose files changed since the merge stopped (say, edited in Unity).
        /// Choosing a side overwrites them, so the caller asks first.
        /// </summary>
        public static async Task<IReadOnlyList<string>> FindEditedAsync(IGitRepository repository, IReadOnlyList<ConflictEntry> entries, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var edited = new List<string>();
            foreach (var entry in entries)
            {
                var hash = await repository.HashWorkingFileAsync(entry.Path, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(hash, ShownHash(entry), StringComparison.Ordinal)) edited.Add(entry.Path);
            }
            return edited;
        }

        /// <summary>Decides <paramref name="entries"/> for <paramref name="side"/>: its content is written and staged, or the file is deleted.</summary>
        public static async Task ResolveAsync(IGitRepository repository, IReadOnlyList<ConflictEntry> entries, ConflictSide side, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var keep = new List<string>();
            var remove = new List<string>();
            foreach (var entry in entries) (entry.IsDeletedOn(side) ? remove : keep).Add(entry.Path);
            if (keep.Count > 0)
            {
                await repository.CheckoutSideAsync(keep, side, cancellationToken).ConfigureAwait(false);
                await repository.StageAsync(keep, cancellationToken).ConfigureAwait(false);
            }
            if (remove.Count > 0) await repository.RemovePathsAsync(remove, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Records the merge once nothing is undecided. Returns the new commit, or null while conflicts remain.</summary>
        public static async Task<string> FinishAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var state = await GetStateAsync(repository, cancellationToken).ConfigureAwait(false);
            if (!state.ReadyToFinish) return null;
            return await repository.CommitMergeAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
