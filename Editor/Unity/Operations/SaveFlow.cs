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
            var repository = session.Repository;
            // Inspector edits are not on disk yet; git must see what the user sees.
            UnitySaver.SaveForSnapshot();
            await session.RunBeforeSaveAsync(cancellationToken);
            await repository.AddAllAsync(cancellationToken);
            var staged = await repository.GetStatusAsync(cancellationToken);
            string hash = null;
            if (staged.HasChanges)
            {
                hash = await repository.CommitAsync(SnapshotMessage.Resolve(message, staged.Stats), cancellationToken);
            }
            await session.RunAfterSaveAsync(hash, cancellationToken);
            return hash;
        }
    }
}
