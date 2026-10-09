using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// 取り出す with the editor kept consistent, like <see cref="RestoreOperation"/>: auto-refresh is
    /// suspended while git writes files, the asset database is refreshed synchronously afterwards, and
    /// open scenes that changed are reloaded. The caller shows the confirmation dialog first.
    /// </summary>
    internal static class SetAsideOperation
    {
        public static async Task<SetAsidePlan> RunAsync(IGitRepository repository, StashEntry stash, CancellationToken cancellationToken)
        {
            if (!MainThread.IsMainThread) throw new InvalidOperationException("SetAsideOperation must start on the main thread");

            SetAsidePlan plan;
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                plan = await SetAsideRunner.ApplyAsync(repository, stash, cancellationToken);
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            if (plan.CanApply) RestoreOperation.ReloadOpenScenes(ScenePaths(plan.Paths));
            return plan;
        }

        internal static IReadOnlyList<string> ScenePaths(IReadOnlyList<string> paths)
        {
            var scenes = new List<string>();
            foreach (var path in paths)
            {
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) scenes.Add(path);
            }
            return scenes;
        }
    }
}
