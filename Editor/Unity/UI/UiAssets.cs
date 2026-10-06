using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>Loads UXML / USS shipped inside the package.</summary>
    internal static class UiAssets
    {
        public const string Root = "Packages/com.yaito3014.shiori/Editor/Unity/UI/";

        public static VisualTreeAsset Tree(string fileName)
        {
            return Load<VisualTreeAsset>(fileName);
        }

        public static StyleSheet Style(string fileName)
        {
            return Load<StyleSheet>(fileName);
        }

        private static T Load<T>(string fileName) where T : UnityEngine.Object
        {
            var path = Root + fileName;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Shiori UI asset not found: " + path);
            return asset;
        }
    }
}
