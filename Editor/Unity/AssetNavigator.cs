using System;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>Reveals the asset behind a repository path in the Project window.</summary>
    internal static class AssetNavigator
    {
        /// <summary>
        /// Maps a repository-relative path to an asset path Unity can load: paths under Assets/ or
        /// Packages/, with a trailing .meta stripped. Returns null for anything else (ProjectSettings/ etc.).
        /// </summary>
        internal static string ToAssetPath(string repositoryPath)
        {
            if (string.IsNullOrEmpty(repositoryPath)) return null;
            var path = repositoryPath;
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - ".meta".Length);
            if (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)) return path;
            return null;
        }

        /// <summary>Selects and pings the asset when it exists in the project. Returns false otherwise (deleted files, settings).</summary>
        public static bool Reveal(string repositoryPath)
        {
            var assetPath = ToAssetPath(repositoryPath);
            if (assetPath == null) return false;
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null) return false;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return true;
        }
    }
}
