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
        private const string HiddenClass = "shiori-hidden";
        private const string ModeActiveClass = "shiori-mode-button--active";

        private EditorStateGuard _guard;
        private ShioriSession _session;
        private IShioriView _mainView;
        private VisualElement _modeBar;
        private VisualElement _modeToggle;
        private Button _modeSimple;
        private Button _modeDetail;
        private Label _statusChip;
        private VisualElement _lockBanner;
        private Label _lockMessage;
        private VisualElement _content;
        private int _refreshGeneration;
        private IVisualElementScheduledItem _scheduledStatusRefresh;

        // Survive domain reloads (F6).
        [SerializeField] private string _selectedHash;
        [SerializeField] private string _draftMessage;
        [SerializeField] private string _selectedPath;

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
            EditorApplication.projectChanged += OnProjectChanged;
            AssetSaveWatcher.AssetsSaved += OnProjectChanged;
            ShioriSettingsEvents.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            _refreshGeneration++;
            ShioriSettingsEvents.Changed -= OnSettingsChanged;
            AssetSaveWatcher.AssetsSaved -= OnProjectChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
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

            _modeBar = rootVisualElement.Q<VisualElement>("mode-bar");
            _modeToggle = rootVisualElement.Q<VisualElement>("mode-toggle");
            _statusChip = rootVisualElement.Q<Label>("status-chip");
            rootVisualElement.Q<Label>("mode-label").text = L10n.Tr("mode.label");
            _modeSimple = rootVisualElement.Q<Button>("mode-simple");
            _modeSimple.text = L10n.Tr("mode.simple");
            _modeSimple.clicked += () => SwitchMode(UiMode.Simple);
            _modeDetail = rootVisualElement.Q<Button>("mode-detail");
            _modeDetail.text = L10n.Tr("mode.detail");
            _modeDetail.clicked += () => SwitchMode(UiMode.Detail);
            _lockBanner = rootVisualElement.Q<VisualElement>("lock-banner");
            _lockMessage = rootVisualElement.Q<Label>("lock-message");
            _content = rootVisualElement.Q<VisualElement>("content");

            ApplyLock(_guard != null ? _guard.Current : LockReason.None);
            Refresh();
        }

        private void OnFocus()
        {
            if (_mainView == null) return;
            RenderHeader();
            _mainView.RefreshAll();
        }

        /// <summary>
        /// The header shows the mode toggle (when both modes are available) and the extensions' status
        /// chip (for example the build target). It is hidden when neither has anything to show.
        /// </summary>
        private void RenderHeader()
        {
            if (_session == null || _modeBar == null) return;
            var canSwitch = _session.User.CanSwitchMode;
            var status = _session.GetStatusLine();
            _modeToggle.EnableInClassList(HiddenClass, !canSwitch);
            _statusChip.text = status ?? string.Empty;
            _statusChip.EnableInClassList(HiddenClass, string.IsNullOrEmpty(status));
            _modeBar.EnableInClassList(HiddenClass, !canSwitch && string.IsNullOrEmpty(status));
        }

        /// <summary>A settings page changed the git path, the mode or the setup flag: start over.</summary>
        private void OnSettingsChanged()
        {
            if (_content != null) Refresh();
        }

        /// <summary>Asset changes and saves arrive in bursts; wait half a second before re-reading the working tree.</summary>
        private void OnProjectChanged()
        {
            if (_mainView == null || _scheduledStatusRefresh != null) return;
            _scheduledStatusRefresh = rootVisualElement.schedule.Execute(() =>
            {
                _scheduledStatusRefresh = null;
                if (_mainView == null) return;
                RenderHeader();
                _mainView.RefreshStatus();
            }).StartingIn(500);
        }

        /// <summary>Switches between かんたん and 詳細, remembering the choice in UserSettings/Shiori.json.</summary>
        private void SwitchMode(UiMode mode)
        {
            if (_session == null || _mainView == null || !_session.User.CanSwitchMode || _session.User.Mode == mode) return;
            _session.User.Mode = mode;
            try
            {
                _session.SaveUserSettings();
            }
            catch (Exception ex)
            {
                ShowError(L10n.Tr("error.generic", ex.Message));
                return;
            }
            _content.Clear();
            try
            {
                ShowMain();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                ShowError(L10n.Tr("error.generic", ex.Message));
            }
        }

        /// <summary>Re-evaluates the project and shows either the wizard or the main view.</summary>
        private async void Refresh()
        {
            var generation = ++_refreshGeneration;
            if (_content == null) return;
            _mainView = null;
            _modeBar.EnableInClassList(HiddenClass, true);
            _content.Clear();
            var loading = new Label(L10n.Tr("status.loading"));
            loading.AddToClassList("shiori-loading");
            _content.Add(loading);

            try
            {
                _session = new ShioriSession(ShioriSession.DetectProjectRoot());
                var status = await _session.EvaluateSetupAsync(CancellationToken.None);
                if (generation != _refreshGeneration || _content == null) return;
                _content.Clear();

                // After setup, a pending extension step (say, a newly added VCC package) is handled from
                // Project Settings rather than by pulling the user back into the wizard.
                if (!_session.Project.SetupCompleted || !status.CoreComplete)
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
                Debug.LogException(ex);
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
            var mode = _session.User.EffectiveMode;
            RenderHeader();
            _modeSimple.EnableInClassList(ModeActiveClass, mode == UiMode.Simple);
            _modeDetail.EnableInClassList(ModeActiveClass, mode == UiMode.Detail);

            if (mode == UiMode.Detail)
            {
                var detail = new DetailModeView(_session);
                detail.RestoreSelection(_selectedPath);
                detail.SelectionChanged += path => _selectedPath = path;
                _content.Add(detail);
                _mainView = detail;
                detail.RefreshAll();
                return;
            }

            var view = new SimpleModeView(_session);
            view.DraftMessage = _draftMessage;
            view.RestoreSelection(_selectedHash);
            view.SelectionChanged += hash => _selectedHash = hash;
            view.DraftChanged += message => _draftMessage = message;
            _content.Add(view);
            _mainView = view;
            view.RefreshAll();
        }

        private void ShowError(string message)
        {
            if (_content == null) return;
            _mainView = null;
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
