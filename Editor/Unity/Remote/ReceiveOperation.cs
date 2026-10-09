using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// 受信 with the editor kept consistent, like 戻す: auto-refresh is suspended while git moves the
    /// working tree forward, the asset database is refreshed synchronously afterwards, and open scenes
    /// that changed are reloaded. The caller has flushed unsaved editor edits and asked for confirmation.
    /// </summary>
    internal static class ReceiveOperation
    {
        public static async Task<ReceiveResult> RunAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (!MainThread.IsMainThread) throw new InvalidOperationException("ReceiveOperation must start on the main thread");

            ReceiveResult result;
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                result = await ReceiveRunner.ReceiveAsync(repository, cancellationToken);
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            if (result.Outcome == ReceiveOutcome.Received) RestoreOperation.ReloadOpenScenes(SetAsideOperation.ScenePaths(result.ChangedPaths));
            return result;
        }
    }
}
