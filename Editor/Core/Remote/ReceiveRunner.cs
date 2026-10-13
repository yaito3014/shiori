using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    public enum ReceiveOutcome
    {
        /// <summary>The project moved forward to the 送信先's latest save.</summary>
        Received,

        /// <summary>The 送信先 has nothing this PC lacks.</summary>
        NothingToReceive,

        /// <summary>No 送信先 is set.</summary>
        NoRemote,

        /// <summary>There are unsaved changes; they must be saved first so nothing is overwritten.</summary>
        UnsavedChanges,

        /// <summary>This PC and the 送信先 both had saves the other lacked; they were merged into a new save.</summary>
        Merged,

        /// <summary>
        /// The histories were split and some files were changed on both sides in ways that could not be combined. The
        /// merge is in progress; each file in <see cref="ReceiveResult.Conflicts"/> shows this PC's version until decided.
        /// </summary>
        Conflicted,

        /// <summary>An earlier merge is still waiting for decisions. Nothing was changed.</summary>
        MergeInProgress,

        /// <summary>The 送信先 holds a different project's history. Nothing was changed.</summary>
        Unrelated,

        /// <summary>Nothing has been saved on this PC yet, or HEAD is not on a branch.</summary>
        NothingSaved,
    }

    public sealed class ReceiveResult
    {
        public ReceiveOutcome Outcome { get; }

        /// <summary>Saves received from the 送信先 (for <see cref="ReceiveOutcome.Received"/>, <see cref="ReceiveOutcome.Merged"/> and <see cref="ReceiveOutcome.Conflicted"/>).</summary>
        public int Count { get; }

        /// <summary>Files the 受信 may have changed in the working tree, repository-relative.</summary>
        public IReadOnlyList<string> ChangedPaths { get; }

        /// <summary>For <see cref="ReceiveOutcome.Conflicted"/>: the files waiting for 自分 / 相手.</summary>
        public IReadOnlyList<ConflictEntry> Conflicts { get; }

        public ReceiveResult(ReceiveOutcome outcome, int count = 0, IReadOnlyList<string> changedPaths = null, IReadOnlyList<ConflictEntry> conflicts = null)
        {
            Outcome = outcome;
            Count = count;
            ChangedPaths = changedPaths ?? Array.Empty<string>();
            Conflicts = conflicts ?? Array.Empty<ConflictEntry>();
        }
    }

    /// <summary>What the 送信先 has compared with this PC, as of the last fetch (no network used to compute it).</summary>
    public sealed class RemoteComparison
    {
        /// <summary>Saves here that are not at the 送信先.</summary>
        public int Ahead { get; }

        /// <summary>Saves at the 送信先 that are not here.</summary>
        public int Behind { get; }

        /// <summary>False when the 送信先 has never been fetched or has no history for this branch.</summary>
        public bool Known { get; }

        public bool Diverged => Ahead > 0 && Behind > 0;

        public RemoteComparison(bool known, int ahead, int behind)
        {
            Known = known;
            Ahead = ahead;
            Behind = behind;
        }

        public static readonly RemoteComparison Unknown = new RemoteComparison(false, 0, 0);
    }

    /// <summary>
    /// 受信: fetch, then fast-forward, or merge when both sides saved separately (M5, see <see cref="MergeRunner"/>).
    /// Never rebases or overwrites unsaved work: unsaved changes block it before anything is fetched. The caller keeps
    /// the editor consistent around the working-tree change.
    /// </summary>
    internal static class ReceiveRunner
    {
        private static string TrackingRef(string branch) => "refs/remotes/" + GitRepository.RemoteName + "/" + branch;

        /// <summary>Compares HEAD with the last fetched state of the 送信先. Local only.</summary>
        public static async Task<RemoteComparison> CompareAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.Branch == null || status.HeadHash == null) return RemoteComparison.Unknown;
            var tracking = await repository.GetRemoteTrackingHashAsync(status.Branch, cancellationToken).ConfigureAwait(false);
            if (tracking == null) return RemoteComparison.Unknown;
            if (!await repository.HaveCommonHistoryAsync("HEAD", tracking, cancellationToken).ConfigureAwait(false)) return RemoteComparison.Unknown;
            var ahead = await repository.CountCommitsAsync(tracking + "..HEAD", cancellationToken).ConfigureAwait(false);
            var behind = await repository.CountCommitsAsync("HEAD.." + tracking, cancellationToken).ConfigureAwait(false);
            return new RemoteComparison(true, ahead, behind);
        }

        /// <summary>
        /// Background check: a non-interactive fetch, then <see cref="CompareAsync"/>. Throws
        /// <see cref="RemoteOperationException"/> when the 送信先 cannot be reached without asking the user.
        /// </summary>
        public static async Task<RemoteComparison> CheckAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (await repository.GetRemoteUrlAsync(cancellationToken).ConfigureAwait(false) == null) return RemoteComparison.Unknown;
            await repository.FetchAsync(false, cancellationToken).ConfigureAwait(false);
            return await CompareAsync(repository, cancellationToken).ConfigureAwait(false);
        }

        public static Task<ReceiveResult> ReceiveAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            return ReceiveAsync(repository, null, cancellationToken);
        }

        /// <param name="mergeMessage">The message of the merge save when histories split; null keeps git's own.</param>
        public static async Task<ReceiveResult> ReceiveAsync(IGitRepository repository, string mergeMessage, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (await repository.GetRemoteUrlAsync(cancellationToken).ConfigureAwait(false) == null) return new ReceiveResult(ReceiveOutcome.NoRemote);
            if (await repository.IsMergingAsync(cancellationToken).ConfigureAwait(false)) return new ReceiveResult(ReceiveOutcome.MergeInProgress);

            var status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.Branch == null || status.HeadHash == null) return new ReceiveResult(ReceiveOutcome.NothingSaved);
            if (status.HasChanges) return new ReceiveResult(ReceiveOutcome.UnsavedChanges);

            await repository.FetchAsync(true, cancellationToken).ConfigureAwait(false);

            var tracking = await repository.GetRemoteTrackingHashAsync(status.Branch, cancellationToken).ConfigureAwait(false);
            if (tracking == null) return new ReceiveResult(ReceiveOutcome.NothingToReceive);
            if (!await repository.HaveCommonHistoryAsync("HEAD", tracking, cancellationToken).ConfigureAwait(false)) return new ReceiveResult(ReceiveOutcome.Unrelated);

            var ahead = await repository.CountCommitsAsync(tracking + "..HEAD", cancellationToken).ConfigureAwait(false);
            var behind = await repository.CountCommitsAsync("HEAD.." + tracking, cancellationToken).ConfigureAwait(false);
            if (behind == 0) return new ReceiveResult(ReceiveOutcome.NothingToReceive);

            var before = status.HeadHash;
            var changed = await repository.GetChangedPathsAsync(before, tracking, cancellationToken).ConfigureAwait(false);
            if (ahead > 0)
            {
                var conflicts = await MergeRunner.StartAsync(repository, TrackingRef(status.Branch), mergeMessage, cancellationToken).ConfigureAwait(false);
                return conflicts.Count == 0
                    ? new ReceiveResult(ReceiveOutcome.Merged, behind, changed)
                    : new ReceiveResult(ReceiveOutcome.Conflicted, behind, changed, conflicts);
            }

            await repository.FastForwardAsync(status.Branch, cancellationToken).ConfigureAwait(false);
            return new ReceiveResult(ReceiveOutcome.Received, behind, changed);
        }
    }
}
