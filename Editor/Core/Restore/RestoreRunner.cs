using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>
    /// The git half of 戻す (F4): secure the current working tree, make the tree match an older
    /// snapshot with <c>read-tree -u --reset</c>, and record the result as a new commit so history
    /// stays linear. Never uses <c>reset --hard</c> or <c>checkout -- .</c>.
    /// The caller must have obtained the user's explicit confirmation before calling this.
    /// </summary>
    internal static class RestoreRunner
    {
        public const string AutoStashMessage = SetAsideChange.Marker;
        public const string RestoreMessagePrefix = "Restore: ";

        public static async Task<RestoreResult> RunAsync(IGitRepository repository, string targetHash, string targetMessage, RestoreMode mode, string memo, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (string.IsNullOrWhiteSpace(targetHash)) throw new ArgumentException("target hash is required", nameof(targetHash));

            string savedHash = null;
            string stashHash = null;

            var before = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (before.HasChanges)
            {
                if (mode == RestoreMode.SaveFirst)
                {
                    await repository.AddAllAsync(cancellationToken).ConfigureAwait(false);
                    var staged = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                    if (staged.HasChanges)
                    {
                        savedHash = await repository.CommitAsync(SnapshotMessage.Resolve(memo, staged.Stats), cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    // The target's memo goes into the stash subject so the list can say what was set aside for which 戻す.
                    stashHash = await repository.StashPushAsync(SetAsideChange.MessageFor(targetMessage ?? targetHash), true, cancellationToken).ConfigureAwait(false);
                }
            }

            await repository.ReadTreeAsync(targetHash, cancellationToken).ConfigureAwait(false);

            var after = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            string restoreHash = null;
            if (after.HasChanges)
            {
                restoreHash = await repository.CommitAsync(RestoreMessagePrefix + (targetMessage ?? targetHash), cancellationToken).ConfigureAwait(false);
            }

            return new RestoreResult(restoreHash, savedHash, stashHash, after.Changes);
        }
    }
}
