using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>Hosts every Shiori view. Shows the lock banner (F6) and disables the content while locked.</summary>
    internal sealed class ShioriWindow : EditorWindow
    {
        private const string PackageRoot = "Packages/com.yaito3014.shiori/Editor/Unity/UI/";
        private const string LockBannerHiddenClass = "shiori-lock-banner--hidden";
        private const string ContentLockedClass = "shiori-content--locked";

        private EditorStateGuard _guard;
        private VisualElement _lockBanner;
        private Label _lockMessage;
        private VisualElement _content;

        [MenuItem("Window/Shiori")]
        public static void Open()
        {
            var window = GetWindow<ShioriWindow>();
            window.titleContent = new GUIContent(L10n.Tr("window.title"));
            window.minSize = new Vector2(320, 240);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(L10n.Tr("window.title"));
            _guard = new EditorStateGuard();
            _guard.Changed += OnLockChanged;
            GitActivity.Changed += OnGitActivityChanged;
        }

        private void OnDisable()
        {
            GitActivity.Changed -= OnGitActivityChanged;
            if (_guard != null)
            {
                _guard.Changed -= OnLockChanged;
                _guard.Dispose();
                _guard = null;
            }
        }

        private void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PackageRoot + "ShioriWindow.uxml");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageRoot + "ShioriWindow.uss");
            if (tree == null || style == null)
            {
                rootVisualElement.Add(new Label("Shiori: UI assets not found under " + PackageRoot));
                return;
            }

            tree.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(style);

            _lockBanner = rootVisualElement.Q<VisualElement>("lock-banner");
            _lockMessage = rootVisualElement.Q<Label>("lock-message");
            _content = rootVisualElement.Q<VisualElement>("content");

            ApplyLock(_guard != null ? _guard.Current : LockReason.None);
        }

        private void OnGitActivityChanged()
        {
            _guard?.Refresh();
        }

        private void OnLockChanged(LockReason reason)
        {
            ApplyLock(reason);
        }

        private void ApplyLock(LockReason reason)
        {
            if (_lockBanner == null || _content == null) return;

            var locked = reason != LockReason.None;
            _lockBanner.EnableInClassList(LockBannerHiddenClass, !locked);
            _lockMessage.text = locked ? L10n.Tr(MessageKey(reason)) : string.Empty;
            _content.SetEnabled(!locked);
            _content.EnableInClassList(ContentLockedClass, locked);
        }

        internal static string MessageKey(LockReason reason)
        {
            switch (reason)
            {
                case LockReason.Compiling: return "lock.compiling";
                case LockReason.Updating: return "lock.updating";
                case LockReason.PlayMode: return "lock.playmode";
                case LockReason.GitBusy: return "lock.gitbusy";
                default: return string.Empty;
            }
        }
    }
}
