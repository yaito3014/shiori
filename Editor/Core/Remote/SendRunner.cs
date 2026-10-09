using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    public enum SendOutcome
    {
        /// <summary>The saves were sent.</summary>
        Sent,

        /// <summary>Everything is already at the 送信先.</summary>
        NothingToSend,

        /// <summary>No 送信先 is set.</summary>
        NoRemote,

        /// <summary>Nothing has been saved yet, or HEAD is not on a branch.</summary>
        NothingSaved,
    }

    public sealed class SendResult
    {
        public SendOutcome Outcome { get; }

        /// <summary>Saves sent (for <see cref="SendOutcome.Sent"/>).</summary>
        public int Count { get; }

        /// <summary>True when the working tree had changes; they are not sent until saved.</summary>
        public bool HadUnsavedChanges { get; }

        public SendResult(SendOutcome outcome, int count, bool hadUnsavedChanges)
        {
            Outcome = outcome;
            Count = count;
            HadUnsavedChanges = hadUnsavedChanges;
        }
    }

    /// <summary>What the 送信 panel shows: whether a 送信先 exists and how many saves are not there yet.</summary>
    public sealed class SendStatus
    {
        public string RemoteUrl { get; }

        public bool HasRemote => RemoteUrl != null;

        /// <summary>Saves not yet at the 送信先, as of the last 送信 (no network is used to compute this).</summary>
        public int Unsent { get; }

        /// <summary>True until the first successful 送信 to this 送信先.</summary>
        public bool NeverSent { get; }

        public SendStatus(string remoteUrl, int unsent, bool neverSent)
        {
            RemoteUrl = remoteUrl;
            Unsent = unsent;
            NeverSent = neverSent;
        }
    }

    /// <summary>
    /// 送信 (M2, send half): pushes the current branch to <c>origin</c>, never forced. A rejection
    /// because the 送信先 has other saves surfaces as <see cref="RemoteOperationException"/> with
    /// <see cref="RemoteErrorKind.Rejected"/>; nothing is merged here (受信 comes later).
    /// </summary>
    internal static class SendRunner
    {
        public static async Task<SendStatus> GetStatusAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var url = await repository.GetRemoteUrlAsync(cancellationToken).ConfigureAwait(false);
            var status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (url == null || status.Branch == null || status.HeadHash == null) return new SendStatus(url, 0, true);

            var tracking = await repository.GetRemoteTrackingHashAsync(status.Branch, cancellationToken).ConfigureAwait(false);
            var unsent = await repository.CountCommitsAsync(tracking == null ? "HEAD" : tracking + "..HEAD", cancellationToken).ConfigureAwait(false);
            return new SendStatus(url, unsent, tracking == null);
        }

        public static async Task<SendResult> SendAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            var dirty = status.HasChanges;
            var url = await repository.GetRemoteUrlAsync(cancellationToken).ConfigureAwait(false);
            if (url == null) return new SendResult(SendOutcome.NoRemote, 0, dirty);
            if (status.Branch == null || status.HeadHash == null) return new SendResult(SendOutcome.NothingSaved, 0, dirty);

            var before = await GetStatusAsync(repository, cancellationToken).ConfigureAwait(false);
            if (!before.NeverSent && before.Unsent == 0) return new SendResult(SendOutcome.NothingToSend, 0, dirty);

            await repository.PushAsync(status.Branch, cancellationToken).ConfigureAwait(false);
            return new SendResult(SendOutcome.Sent, before.Unsent, dirty);
        }
    }

    public enum RemoteCheckState
    {
        /// <summary>Reachable and empty: ready for the first 送信.</summary>
        Empty,

        /// <summary>Reachable and holds only saves this project already has.</summary>
        SameHistory,

        /// <summary>Reachable but holds saves this project does not know: maybe another project, maybe another PC.</summary>
        OtherHistory,
    }

    /// <summary>「確認する」: checks a 送信先 URL with <c>ls-remote</c> without writing anything anywhere.</summary>
    internal static class RemoteChecker
    {
        public static async Task<RemoteCheckState> CheckAsync(IGitRepository repository, string url, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var heads = await repository.ListRemoteHeadsAsync(url, cancellationToken).ConfigureAwait(false);
            if (heads.Count == 0) return RemoteCheckState.Empty;
            foreach (var pair in heads)
            {
                if (!await repository.RevisionExistsAsync(pair.Value, cancellationToken).ConfigureAwait(false)) return RemoteCheckState.OtherHistory;
            }
            return RemoteCheckState.SameHistory;
        }
    }
}
