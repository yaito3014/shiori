using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori.Editor
{
    /// <summary>
    /// 保存 / commit, shared by simple and detail mode so both run the same steps: flush Unity's
    /// in-memory edits, run the extensions' before-save hooks, stage everything, commit, then the
    /// after-save hooks. An empty message gets the generated "Snapshot: …" text.
    /// </summary>
    internal static class SaveFlow
    {
        /// <summary>The new commit hash, or null when there was nothing to save. The caller shows progress and locks the UI.</summary>
        public static async Task<string> RunAsync(ShioriSession session, string message, CancellationToken cancellationToken)
        {
            return await RunAsync(session, message, stageAll: true, cancellationToken);
        }

        /// <summary>
        /// Detail mode's commit of what the user staged: the same steps without <c>git add -A</c>, so
        /// unstaged changes stay out (files the before-save hooks write stay unstaged too).
        /// Null when nothing is staged.
        /// </summary>
        public static async Task<string> RunStagedAsync(ShioriSession session, string message, CancellationToken cancellationToken)
        {
            return await RunAsync(session, message, stageAll: false, cancellationToken);
        }

        private static async Task<string> RunAsync(ShioriSession session, string message, bool stageAll, CancellationToken cancellationToken)
        {
            var repository = session.Repository;
            // Inspector edits are not on disk yet; git must see what the user sees.
            UnitySaver.SaveForSnapshot();
            await session.RunBeforeSaveAsync(cancellationToken);
            if (stageAll) await repository.AddAllAsync(cancellationToken);
            var staged = StagedOnly(await repository.GetStatusAsync(cancellationToken));
            string hash = null;
            if (staged.Count > 0)
            {
                hash = await repository.CommitAsync(SnapshotMessage.Resolve(message, ChangeStats.From(staged)), cancellationToken);
            }
            await session.RunAfterSaveAsync(hash, cancellationToken);
            return hash;
        }

        /// <summary>The staged side of each change, classified by its index letter alone.</summary>
        internal static List<FileChange> StagedOnly(WorktreeStatus status)
        {
            var staged = new List<FileChange>();
            foreach (var change in status.Changes)
            {
                if (!change.IsStaged) continue;
                staged.Add(new FileChange(change.Path, StatusParser.Classify(change.IndexStatus, '.'), change.IndexStatus, '.', change.OldPath));
            }
            return staged;
        }
    }
}
