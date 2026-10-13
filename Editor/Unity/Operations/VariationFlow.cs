using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// Switching, deleting and merging branches with the editor kept consistent, shared by simple mode
    /// (バリエーション, <paramref name="gitTerms"/> false) and detail mode's branch tab (git terms). Each one asks
    /// the user first; that dialog is the confirmation the safety rules require.
    /// </summary>
    internal static class VariationFlow
    {
        /// <summary>The name to show: 「メイン」 for main / master in simple mode, the bare variation name otherwise.</summary>
        public static string NameOf(BranchInfo branch, bool gitTerms)
        {
            if (gitTerms) return branch.Name;
            return VariationNames.IsMain(branch.Name) ? L10n.Tr("variation.main") : VariationNames.Display(branch.Name);
        }

        private static string Key(bool gitTerms, string key) => (gitTerms ? "branch." : "variation.") + key;

        /// <summary>
        /// Switches to <paramref name="target"/>. With unsaved changes the user picks between saving them into the
        /// current variation first and carrying them along. Returns null when the user cancelled.
        /// </summary>
        public static async Task<SwitchResult> SwitchAsync(ShioriSession session, BranchInfo target, string memo, bool gitTerms, CancellationToken cancellationToken)
        {
            var repository = session.Repository;
            // Open scenes may be replaced by the other variation's files; their edits must reach disk first.
            if (!UnitySaver.SaveForRestoreOrCancel()) return null;

            var status = await repository.GetStatusAsync(cancellationToken);
            var preview = await session.PreviewSwitchAsync(target, cancellationToken);
            var text = new StringBuilder(L10n.Tr(Key(gitTerms, "switch.body"), NameOf(target, gitTerms)));
            if (preview.ChangedPaths.Count > 0) text.Append("\n\n").Append(L10n.Tr("variation.switch.reimport", preview.ChangedPaths.Count));
            var warning = session.Extensions.Count > 0 ? await session.GetSwitchWarningAsync(preview, cancellationToken) : null;
            var body = RestoreFlow.WithWarning(text.ToString(), warning);

            SwitchMode mode;
            var title = L10n.Tr(Key(gitTerms, "switch.title"));
            if (status.HasChanges)
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    title,
                    L10n.Tr(Key(gitTerms, "switch.dirty")) + "\n\n" + body,
                    L10n.Tr(Key(gitTerms, "switch.savefirst")),
                    L10n.Tr("restore.dialog.cancel"),
                    L10n.Tr(Key(gitTerms, "switch.carry")));
                if (choice == 1) return null;
                mode = choice == 0 ? SwitchMode.SaveFirst : SwitchMode.Carry;
            }
            else
            {
                if (!EditorUtility.DisplayDialog(title, body, L10n.Tr(Key(gitTerms, "switch.ok")), L10n.Tr("restore.dialog.cancel"))) return null;
                mode = SwitchMode.SaveFirst;
            }

            SwitchResult result;
            using (GitActivity.Begin(L10n.Tr(Key(gitTerms, "switch.progress"))))
            {
                result = await WithAssetsHeldAsync(() => VariationRunner.SwitchAsync(repository, target, mode, memo, cancellationToken));
            }
            RestoreOperation.ReloadOpenScenes(result.ChangedScenePaths);
            return result;
        }

        public static string DescribeSwitch(BranchInfo target, SwitchResult result, bool gitTerms)
        {
            if (result.Outcome == SwitchOutcome.Blocked)
            {
                return L10n.Tr(Key(gitTerms, "switch.blocked"), SimpleModeView.ListPaths(result.BlockingPaths, 5));
            }
            var sb = new StringBuilder(L10n.Tr(Key(gitTerms, "switch.done"), NameOf(target, gitTerms)));
            if (result.TouchedProjectSettings) sb.Append('\n').Append(L10n.Tr("variation.switch.projectsettings"));
            return sb.ToString();
        }

        /// <summary>Asks, then deletes <paramref name="branch"/> here and at the 送信先. Returns false when the user cancelled.</summary>
        public static async Task<bool> DeleteAsync(ShioriSession session, BranchInfo branch, bool gitTerms, CancellationToken cancellationToken)
        {
            var repository = session.Repository;
            var only = await VariationRunner.CountOnlyOnAsync(repository, branch, cancellationToken);
            var body = new StringBuilder(L10n.Tr(Key(gitTerms, "delete.body"), NameOf(branch, gitTerms)));
            if (only > 0) body.Append("\n\n").Append(L10n.Tr(Key(gitTerms, "delete.unique"), only));
            if (branch.IsOnRemote) body.Append("\n\n").Append(L10n.Tr(Key(gitTerms, "delete.remote")));
            if (!EditorUtility.DisplayDialog(
                    L10n.Tr(Key(gitTerms, "delete.title")),
                    body.ToString(),
                    L10n.Tr(Key(gitTerms, "delete.ok")),
                    L10n.Tr("restore.dialog.cancel"))) return false;

            using (GitActivity.Begin(L10n.Tr(Key(gitTerms, "delete.progress"))))
            {
                await VariationRunner.DeleteAsync(repository, branch, true, cancellationToken);
            }
            return true;
        }

        /// <summary>Detail mode only: asks, then merges <paramref name="source"/> into the current branch. Null when cancelled.</summary>
        public static async Task<MergeResult> MergeAsync(ShioriSession session, BranchInfo source, string currentBranch, CancellationToken cancellationToken)
        {
            if (!UnitySaver.SaveForRestoreOrCancel()) return null;
            if (!EditorUtility.DisplayDialog(
                    L10n.Tr("branch.merge.title"),
                    L10n.Tr("branch.merge.body", source.IsRemoteOnly ? "origin/" + source.Name : source.Name, currentBranch),
                    L10n.Tr("branch.merge.ok"),
                    L10n.Tr("restore.dialog.cancel"))) return null;

            MergeResult result;
            using (GitActivity.Begin(L10n.Tr("branch.merge.progress")))
            {
                result = await WithAssetsHeldAsync(() => VariationRunner.MergeAsync(session.Repository, source, cancellationToken));
            }
            RestoreOperation.ReloadOpenScenes(result.ChangedScenePaths);
            return result;
        }

        public static string DescribeMerge(BranchInfo source, MergeResult result)
        {
            switch (result.Outcome)
            {
                case MergeOutcome.UpToDate: return L10n.Tr("branch.merge.uptodate", source.Name);
                case MergeOutcome.Unsaved: return L10n.Tr("branch.merge.unsaved");
                case MergeOutcome.Conflicted: return L10n.Tr("branch.merge.conflicted", SimpleModeView.ListPaths(result.ConflictPaths, 5));
                default: return L10n.Tr("branch.merge.done", source.Name, result.ChangedPaths.Count);
            }
        }

        /// <summary>
        /// Files change under Unity's feet: auto-refresh is held while git works and the asset database is
        /// refreshed synchronously afterwards, as for 戻す.
        /// </summary>
        private static async Task<T> WithAssetsHeldAsync<T>(Func<Task<T>> run)
        {
            if (!MainThread.IsMainThread) throw new InvalidOperationException("VariationFlow must start on the main thread");
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                return await run();
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
