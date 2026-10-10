using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>What a 戻す did, or null when the user cancelled.</summary>
    internal sealed class RestoreOutcome
    {
        public RestoreMode Mode { get; }
        public RestoreResult Result { get; }

        public RestoreOutcome(RestoreMode mode, RestoreResult result)
        {
            Mode = mode;
            Result = result;
        }
    }

    /// <summary>
    /// 戻す / restore to a commit, shared by simple and detail mode: flush editor edits, preview the
    /// change for extension warnings, ask the user (the confirmation the safety rules require), run the
    /// restore with the editor kept consistent, and call the after-restore hooks.
    /// </summary>
    internal static class RestoreFlow
    {
        /// <summary>
        /// Runs the whole 戻す to <paramref name="target"/>. <paramref name="memo"/> is used when the user
        /// chooses to save first. Returns null when the user cancelled anywhere. Shows progress itself.
        /// </summary>
        public static async Task<RestoreOutcome> RunAsync(ShioriSession session, Snapshot target, string memo, CancellationToken cancellationToken)
        {
            var repository = session.Repository;
            // In-memory edits are invisible to git and would be written over the restored files later;
            // flush them first (scenes with a prompt, assets silently).
            if (!UnitySaver.SaveForRestoreOrCancel()) return null;

            var status = await repository.GetStatusAsync(cancellationToken);
            string warning = null;
            if (session.Extensions.Count > 0)
            {
                var preview = await session.PreviewRestoreAsync(target, cancellationToken);
                warning = await session.GetRestoreWarningAsync(preview, cancellationToken);
            }
            var mode = AskRestoreMode(target, status.HasChanges, warning);
            if (mode == null) return null;

            RestoreResult result;
            using (GitActivity.Begin(L10n.Tr("restore.progress")))
            {
                result = await RestoreOperation.RunAsync(repository, target.Hash, target.Message, mode.Value, memo, cancellationToken);
                await session.RunAfterRestoreAsync(result, cancellationToken);
            }
            return new RestoreOutcome(mode.Value, result);
        }

        /// <summary>The F4 confirmation. Extension warnings (if any) follow the main text. Returns null when the user cancels.</summary>
        private static RestoreMode? AskRestoreMode(Snapshot target, bool hasChanges, string warning)
        {
            var title = L10n.Tr("restore.dialog.title");
            var when = RelativeTime.Format(target.Time, DateTimeOffset.Now);
            if (hasChanges)
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    title,
                    WithWarning(L10n.Tr("restore.dialog.dirty", target.Message, when), warning),
                    L10n.Tr("restore.dialog.savefirst"),
                    L10n.Tr("restore.dialog.cancel"),
                    L10n.Tr("restore.dialog.discard"));
                switch (choice)
                {
                    case 0: return RestoreMode.SaveFirst;
                    case 2: return RestoreMode.StashFirst;
                    default: return null;
                }
            }

            var ok = EditorUtility.DisplayDialog(
                title,
                WithWarning(L10n.Tr("restore.dialog.clean", target.Message, when), warning),
                L10n.Tr("restore.dialog.ok"),
                L10n.Tr("restore.dialog.cancel"));
            return ok ? RestoreMode.StashFirst : (RestoreMode?)null;
        }

        internal static string WithWarning(string text, string warning)
        {
            return string.IsNullOrEmpty(warning) ? text : text + "\n\n" + warning;
        }

        public static string Describe(Snapshot target, RestoreResult result)
        {
            var sb = new StringBuilder();
            sb.Append(result.ChangedAnything ? L10n.Tr("restore.done", target.Message) : L10n.Tr("restore.nochange", target.Message));
            if (result.StashHash != null) sb.Append('\n').Append(L10n.Tr("restore.stashed"));
            if (result.TouchedProjectSettings) sb.Append('\n').Append(L10n.Tr("restore.projectsettings"));
            return sb.ToString();
        }
    }
}
