using UnityEditor;
using UnityEditor.SceneManagement;

namespace Shiori.Editor
{
    /// <summary>
    /// Flushes Unity's in-memory edits to disk before git looks at the working tree.
    /// Inspector changes to materials, prefabs and other assets are not files until saved.
    /// </summary>
    internal static class UnitySaver
    {
        /// <summary>
        /// Saves modified assets silently (what File > Save does) and asks about modified scenes.
        /// Returns false when the user cancelled the scene prompt; callers should abort then.
        /// </summary>
        public static bool SaveEverythingOrCancel()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
