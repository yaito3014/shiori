using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Shiori.Editor
{
    /// <summary>
    /// Flushes Unity's in-memory edits to disk before git looks at the working tree.
    /// Inspector changes to materials, prefabs and other assets are not files until saved.
    /// Untitled scenes are never touched: they have no file, so git cannot record them anyway.
    /// </summary>
    internal static class UnitySaver
    {
        /// <summary>
        /// For 保存: writes modified assets and modified scenes that already have a file, silently,
        /// the way File > Save does. Nothing to confirm, so it never cancels.
        /// </summary>
        public static void SaveForSnapshot()
        {
            foreach (var scene in DirtyScenesWithPath())
            {
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// For 戻す: modified scenes are about to be reloaded from older files, so the user is asked
        /// whether to keep their edits first. Assets are saved silently. Returns false on cancel.
        /// </summary>
        public static bool SaveForRestoreOrCancel()
        {
            var dirty = DirtyScenesWithPath();
            if (dirty.Count > 0 && !EditorSceneManager.SaveModifiedScenesIfUserWantsTo(dirty.ToArray())) return false;
            AssetDatabase.SaveAssets();
            return true;
        }

        private static List<Scene> DirtyScenesWithPath()
        {
            var result = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty && !string.IsNullOrEmpty(scene.path)) result.Add(scene);
            }
            return result;
        }
    }
}
