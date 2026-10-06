using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>
    /// Hosts every Shiori view. Shows the lock banner (F6), disables the content while locked,
    /// and decides between the setup wizard and the main view.
    /// </summary>
    internal sealed class ShioriWindow : EditorWindow
    {
        private const string LockBannerHiddenClass = "shiori-lock-banner--hidden";
        private const string ContentLockedClass = "shiori-content--locked";

        private EditorStateGuard _guard;
        private ShioriSession _session;
        private VisualElement _lockBanner;
        private Label _lockMessage;
        private VisualElement _content;
        private int _refreshGeneration;

        [MenuItem("Window/Shiori")]
        public static void Open()
        {
            var window = GetWindow<ShioriWindow>();
            window.titleContent = new GUIContent(L10n.Tr("window.title"));
            window.minSize = new Vector2(360, 300);
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
            _refreshGeneration++;
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
            try
            {
                UiAssets.Tree("ShioriWindow.uxml").CloneTree(rootVisualElement);
                rootVisualElement.styleSheets.Add(UiAssets.Style("ShioriWindow.uss"));
            }
            catch (InvalidOperationException ex)
            {
                rootVisualElement.Add(new Label(ex.Message));
                return;
            }

            _lockBanner = rootVisualElement.Q<VisualElement>("lock-banner");
            _lockMessage = rootVisualElement.Q<Label>("lock-message");
            _content = rootVisualElement.Q<VisualElement>("content");

            ApplyLock(_guard != null ? _guard.Current : LockReason.None);
            Refresh();
        }

        /// <summary>Re-evaluates the project and shows either the wizard or the main view.</summary>
        private async void Refresh()
        {
            var generation = ++_refreshGeneration;
            if (_content == null) return;
            _content.Clear();

            try
            {
                _session = new ShioriSession(ShioriSession.DetectProjectRoot());
                var status = await _session.EvaluateSetupAsync(CancellationToken.None);
                if (generation != _refreshGeneration || _content == null) return;

                if (!_session.Project.SetupCompleted || !status.IsComplete)
                {
                    ShowWizard(status);
                }
                else
                {
                    ShowMain();
                }
            }
            catch (SettingsFormatException ex)
            {
                ShowError(L10n.Tr("error.settings", ex.Message));
            }
            catch (GitException ex)
            {
                ShowError(L10n.Tr("error.git", ex.ExitCode, ex.Stderr.Trim()));
            }
            catch (Exception ex)
            {
                ShowError(L10n.Tr("error.generic", ex.Message));
            }
        }

        private void ShowWizard(SetupStatus status)
        {
            var wizard = new SetupWizardView(_session, status);
            wizard.Completed += Refresh;
            _content.Add(wizard);
        }

        private void ShowMain()
        {
            // Filled in by the simple-mode view (F2-F4).
        }

        private void ShowError(string message)
        {
            if (_content == null) return;
            _content.Clear();
            var label = new Label(message);
            label.AddToClassList("shiori-error");
            _content.Add(label);
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
