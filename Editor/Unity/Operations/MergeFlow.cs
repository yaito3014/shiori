using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// Deciding the conflicts of a stopped merge, giving it up, and finishing it, with the editor kept consistent.
    /// Shared by simple mode (自分 / 相手) and detail mode (ours / theirs, <paramref name="gitTerms"/>). Overwriting a
    /// file the user edited since the merge stopped, and giving up the merge, both ask first; that dialog is the
    /// confirmation the safety rules require.
    /// </summary>
    internal static class MergeFlow
    {
        private static string Key(bool gitTerms, string key) => (gitTerms ? "merge.detail." : "merge.") + key;

        /// <summary>Keeps <paramref name="side"/> for <paramref name="entries"/>. False when the user cancelled.</summary>
        public static async Task<bool> ResolveAsync(ShioriSession session, IReadOnlyList<ConflictEntry> entries, ConflictSide side, bool gitTerms, CancellationToken cancellationToken)
        {
            if (entries.Count == 0) return false;
            // An open scene among them may have edits only in memory; they must reach disk so the check below sees them.
            if (!UnitySaver.SaveForRestoreOrCancel()) return false;

            var repository = session.Repository;
            var edited = await MergeRunner.FindEditedAsync(repository, entries, cancellationToken);
            if (edited.Count > 0 && !EditorUtility.DisplayDialog(
                    L10n.Tr(Key(gitTerms, "edited.title")),
                    L10n.Tr(Key(gitTerms, "edited.body"), SimpleModeView.ListPaths(edited, 5)),
                    L10n.Tr(Key(gitTerms, "edited.ok")),
                    L10n.Tr("restore.dialog.cancel"))) return false;

            var paths = new List<string>(entries.Count);
            foreach (var entry in entries) paths.Add(entry.Path);
            using (GitActivity.Begin(L10n.Tr(Key(gitTerms, "progress"))))
            {
                await VariationFlow.WithAssetsHeldAsync(async () =>
                {
                    await MergeRunner.ResolveAsync(repository, entries, side, cancellationToken);
                    return true;
                });
            }
            RestoreOperation.ReloadOpenScenes(SetAsideOperation.ScenePaths(paths));
            return true;
        }

        /// <summary>Asks, then gives the merge up: files go back to how they were before it started. False when cancelled.</summary>
        public static async Task<bool> AbortAsync(ShioriSession session, bool gitTerms, CancellationToken cancellationToken)
        {
            if (!EditorUtility.DisplayDialog(
                    L10n.Tr(Key(gitTerms, "abort.title")),
                    L10n.Tr(Key(gitTerms, "abort.body")),
                    L10n.Tr(Key(gitTerms, "abort.ok")),
                    L10n.Tr("restore.dialog.cancel"))) return false;

            // Every file the merge may have touched differs between HEAD and what was being merged in.
            var changedPaths = await session.Repository.GetChangedPathsAsync("HEAD", "MERGE_HEAD", cancellationToken);
            using (GitActivity.Begin(L10n.Tr(Key(gitTerms, "progress"))))
            {
                await VariationFlow.WithAssetsHeldAsync(async () =>
                {
                    await session.Repository.AbortMergeAsync(cancellationToken);
                    return true;
                });
            }
            RestoreOperation.ReloadOpenScenes(SetAsideOperation.ScenePaths(changedPaths));
            return true;
        }

        /// <summary>Records the merge once every file is decided. Null while something is still undecided.</summary>
        public static async Task<string> FinishAsync(ShioriSession session, bool gitTerms, CancellationToken cancellationToken)
        {
            using (GitActivity.Begin(L10n.Tr(Key(gitTerms, "progress"))))
            {
                return await MergeRunner.FinishAsync(session.Repository, cancellationToken);
            }
        }
    }
}
