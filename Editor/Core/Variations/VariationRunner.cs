using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>
    /// The git half of バリエーション (M4) and of the detail mode's branch tab: create, switch, delete and merge.
    /// The caller has the user's confirmation for switch, delete and merge, and keeps the editor consistent.
    /// </summary>
    internal static class VariationRunner
    {
        /// <summary>Makes <paramref name="branch"/> at HEAD and switches to it. Unsaved changes stay where they are.</summary>
        public static async Task<CreateOutcome> CreateAsync(IGitRepository repository, string branch, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (branch == null || !await repository.IsValidBranchNameAsync(branch, cancellationToken).ConfigureAwait(false)) return CreateOutcome.InvalidName;
            foreach (var existing in await repository.GetBranchesAsync(cancellationToken).ConfigureAwait(false))
            {
                // Windows keeps loose refs as files, so names differing only in case collide.
                if (string.Equals(existing.Name, branch, StringComparison.OrdinalIgnoreCase)) return CreateOutcome.AlreadyExists;
            }
            await repository.CreateBranchAsync(branch, cancellationToken).ConfigureAwait(false);
            return CreateOutcome.Created;
        }

        public static async Task<SwitchResult> SwitchAsync(IGitRepository repository, BranchInfo target, SwitchMode mode, string memo, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target.IsCurrent) throw new ArgumentException("already on this branch", nameof(target));

            string savedHash = null;
            var status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.HasChanges && mode == SwitchMode.SaveFirst)
            {
                await repository.AddAllAsync(cancellationToken).ConfigureAwait(false);
                var staged = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                if (staged.HasChanges)
                {
                    savedHash = await repository.CommitAsync(SnapshotMessage.Resolve(memo, staged.Stats), cancellationToken).ConfigureAwait(false);
                }
            }

            var before = await repository.GetHeadAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await repository.SwitchBranchAsync(target.Name, target.IsRemoteOnly, cancellationToken).ConfigureAwait(false);
            }
            catch (GitException ex) when (IsOverwriteRefusal(ex.Stderr))
            {
                return new SwitchResult(SwitchOutcome.Blocked, savedHash, null, ListedPaths(ex.Stderr));
            }

            var after = await repository.GetHeadAsync(cancellationToken).ConfigureAwait(false);
            var changed = before != null && after != null && before != after
                ? await repository.GetChangedPathsAsync(before, after, cancellationToken).ConfigureAwait(false)
                : (IReadOnlyList<string>)Array.Empty<string>();
            return new SwitchResult(SwitchOutcome.Switched, savedHash, changed, null);
        }

        /// <summary>
        /// Deletes <paramref name="branch"/> here and, with <paramref name="alsoRemote"/>, at the 送信先 too (first,
        /// so a failing network leaves the local branch in place). Never the current branch.
        /// </summary>
        public static async Task DeleteAsync(IGitRepository repository, BranchInfo branch, bool alsoRemote, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (branch == null) throw new ArgumentNullException(nameof(branch));
            if (branch.IsCurrent) throw new ArgumentException("the current branch cannot be deleted", nameof(branch));
            if (alsoRemote && branch.IsOnRemote) await repository.DeleteRemoteBranchAsync(branch.Name, cancellationToken).ConfigureAwait(false);
            if (!branch.IsRemoteOnly) await repository.DeleteBranchAsync(branch.Name, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Commits on <paramref name="branch"/> that the current branch does not have.</summary>
        public static Task<int> CountOnlyOnAsync(IGitRepository repository, BranchInfo branch, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (branch == null) throw new ArgumentNullException(nameof(branch));
            return repository.CountCommitsAsync("HEAD.." + branch.RefName, cancellationToken);
        }

        /// <summary>
        /// Merges <paramref name="source"/> into the current branch, only on a clean working tree. A conflict is
        /// aborted at once, so the project is never left half-merged (there is no conflict UI before M5).
        /// </summary>
        public static async Task<MergeResult> MergeAsync(IGitRepository repository, BranchInfo source, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (status.HasChanges) return new MergeResult(MergeOutcome.Unsaved, null, null);

            var before = await repository.GetHeadAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await repository.MergeAsync(source.RefName, cancellationToken).ConfigureAwait(false);
            }
            catch (GitException)
            {
                var stopped = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                var conflicts = new List<string>();
                foreach (var change in stopped.Changes)
                {
                    if (change.Kind == ChangeKind.Unmerged) conflicts.Add(change.Path);
                }
                if (conflicts.Count == 0 && !stopped.HasChanges) throw;
                await repository.AbortMergeAsync(cancellationToken).ConfigureAwait(false);
                if (conflicts.Count == 0) throw;
                return new MergeResult(MergeOutcome.Conflicted, null, conflicts);
            }

            var after = await repository.GetHeadAsync(cancellationToken).ConfigureAwait(false);
            if (after == before) return new MergeResult(MergeOutcome.UpToDate, null, null);
            var changed = before == null
                ? await repository.GetTreePathsAsync(after, cancellationToken).ConfigureAwait(false)
                : await repository.GetChangedPathsAsync(before, after, cancellationToken).ConfigureAwait(false);
            return new MergeResult(MergeOutcome.Merged, changed, null);
        }

        /// <summary>Git's (LC_ALL=C) refusal to switch because local changes or untracked files would be overwritten.</summary>
        internal static bool IsOverwriteRefusal(string stderr)
        {
            return stderr != null && stderr.IndexOf("would be overwritten by checkout", StringComparison.Ordinal) >= 0;
        }

        /// <summary>The tab-indented file lines git lists under its refusal.</summary>
        internal static IReadOnlyList<string> ListedPaths(string stderr)
        {
            var paths = new List<string>();
            foreach (var line in (stderr ?? string.Empty).Split('\n'))
            {
                if (line.StartsWith("\t", StringComparison.Ordinal)) paths.Add(line.Trim());
            }
            return paths;
        }
    }
}
