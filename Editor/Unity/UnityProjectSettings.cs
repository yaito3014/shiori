using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>Reads and applies the two project settings version control needs (F1 step 2).</summary>
    internal static class UnityProjectSettings
    {
        public const string VisibleMetaFilesMode = "Visible Meta Files";

        public static bool IsForceText => EditorSettings.serializationMode == SerializationMode.ForceText;

        public static bool IsVisibleMetaFiles => VersionControlSettings.mode == VisibleMetaFilesMode;

        public static bool IsConfigured => IsForceText && IsVisibleMetaFiles;

        public static string SerializationModeName => EditorSettings.serializationMode.ToString();

        public static string VersionControlModeName => VersionControlSettings.mode;

        /// <summary>Sets Force Text and Visible Meta Files, then saves project assets as the spec requires.</summary>
        public static void Apply()
        {
            if (!IsForceText) EditorSettings.serializationMode = SerializationMode.ForceText;
            if (!IsVisibleMetaFiles) VersionControlSettings.mode = VisibleMetaFilesMode;
            AssetDatabase.SaveAssets();
        }
    }
}
