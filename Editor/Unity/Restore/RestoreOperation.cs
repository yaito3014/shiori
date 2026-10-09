using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Shiori.Editor
{
    /// <summary>
    /// 戻す (F4) with the editor kept consistent: auto-refresh is suspended while files change,
    /// the asset database is refreshed synchronously afterwards, and open scenes that changed on
    /// disk are reloaded. The caller shows the confirmation dialog before calling this.
    /// </summary>
    internal static class RestoreOperation
    {
        public static async Task<RestoreResult> RunAsync(IGitRepository repository, string targetHash, string targetMessage, RestoreMode mode, string memo, CancellationToken cancellationToken)
        {
            if (!MainThread.IsMainThread) throw new InvalidOperationException("RestoreOperation must start on the main thread");

            RestoreResult result;
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                result = await RestoreRunner.RunAsync(repository, targetHash, targetMessage, mode, memo, cancellationToken);
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            ReloadOpenScenes(result.ChangedScenePaths);
            return result;
        }

        /// <summary>Reopens the loaded scenes when any of them changed on disk, keeping their order.</summary>
        internal static void ReloadOpenScenes(IReadOnlyList<string> changedScenePaths)
        {
            if (changedScenePaths == null || changedScenePaths.Count == 0) return;

            var changed = new HashSet<string>(changedScenePaths, StringComparer.Ordinal);
            var loaded = new List<string>();
            var anyChanged = false;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) continue;
                loaded.Add(scene.path);
                if (changed.Contains(scene.path)) anyChanged = true;
            }
            if (!anyChanged) return;

            for (var i = 0; i < loaded.Count; i++)
            {
                EditorSceneManager.OpenScene(loaded[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }
        }
    }
}
