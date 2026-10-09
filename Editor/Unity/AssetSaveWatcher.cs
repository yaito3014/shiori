using System;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>
    /// Raises <see cref="AssetsSaved"/> after Unity writes assets to disk (Ctrl+S, File > Save,
    /// AssetDatabase.SaveAssets). Git only sees files on disk, so this is the moment the
    /// working-tree view becomes stale. projectChanged does not fire for plain saves.
    /// </summary>
    internal sealed class AssetSaveWatcher : AssetModificationProcessor
    {
        public static event Action AssetsSaved;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            // Let the save finish first; subscribers read the files.
            EditorApplication.delayCall += () => AssetsSaved?.Invoke();
            return paths;
        }
    }
}
