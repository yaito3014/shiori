using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>
    /// The setup wizard (F1). Steps unlock top to bottom; each re-renders from <see cref="SetupStatus"/>.
    /// Three core steps, then one per installed extension, then the first save.
    /// </summary>
    internal sealed class SetupWizardView : VisualElement
    {
        private const string HiddenClass = "shiori-hidden";

        private readonly ShioriSession _session;
        private readonly VisualTreeAsset _stepTemplate;
        private readonly VisualElement _container;
        private readonly List<StepElement> _steps = new List<StepElement>();
        private readonly VisualElement _finishRow;
        private readonly Label _finishMessage;

        private SetupStatus _status;
        private StepElement _remoteStep;
        private RemoteSetupPanel _remotePanel;
        /// <summary>Set after the first save when no 送信先 exists yet: the wizard stays open to offer the optional step.</summary>
        private bool _offerRemote;
        private TextField _nameField;
        private TextField _emailField;
        private bool _busy;

        /// <summary>Raised after the project settings file records the wizard as completed.</summary>
        public event Action Completed;

        public SetupWizardView(ShioriSession session, SetupStatus status)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _status = status ?? throw new ArgumentNullException(nameof(status));

            UiAssets.Tree("SetupWizardView.uxml").CloneTree(this);
            style.flexGrow = 1;
            _stepTemplate = UiAssets.Tree("SetupStep.uxml");

            this.Q<Label>("wizard-title").text = L10n.Tr("wizard.title");
            this.Q<Label>("wizard-intro").text = L10n.Tr("wizard.intro");
            _finishRow = this.Q<VisualElement>("finish-row");
            _finishMessage = this.Q<Label>("finish-message");
            var finishButton = this.Q<Button>("finish-button");
            finishButton.text = L10n.Tr("wizard.finish.button");
            finishButton.clicked += Finish;

            _container = this.Q<VisualElement>("steps");
            BuildSteps();
            Render();
        }

        private StepElement FirstSaveElement => _steps[_status.FirstSaveStep - 1];

        /// <summary>Creates one element per step. Rebuilt only when the step count changes (it should not within a domain).</summary>
        private void BuildSteps()
        {
            if (_steps.Count == _status.StepCount) return;
            _container.Clear();
            _steps.Clear();
            for (var number = 1; number <= _status.StepCount; number++)
            {
                var element = new StepElement(_stepTemplate, number, TitleOf(number));
                _steps.Add(element);
                _container.Add(element.Root);
            }
            // The optional 送信先 step comes last and is not part of the gating.
            _remoteStep = new StepElement(_stepTemplate, _status.StepCount + 1, L10n.Tr("step.remote.title"));
            _remotePanel = null;
            _container.Add(_remoteStep.Root);
        }

        private string TitleOf(int number)
        {
            switch (number)
            {
                case SetupStatus.GitStep: return L10n.Tr("step1.title");
                case SetupStatus.ProjectSettingsStep: return L10n.Tr("step2.title");
                case SetupStatus.IgnoreFilesStep: return L10n.Tr("step3.title");
            }
            if (_status.IsExtensionStep(number)) return _status.GetExtensionStep(number).Title;
            return L10n.Tr("step4.title");
        }

        private void Render()
        {
            BuildSteps();
            for (var i = 0; i < _steps.Count; i++)
            {
                var number = i + 1;
                _steps[i].SetState(_status.IsStepDone(number), _status.IsStepEnabled(number), _status.CurrentStep == number);
                _steps[i].ClearControls();
            }

            RenderGitStep(_steps[0], _status.IsStepEnabled(SetupStatus.GitStep));
            RenderProjectSettingsStep(_steps[1], _status.IsStepEnabled(SetupStatus.ProjectSettingsStep));
            RenderIgnoreFilesStep(_steps[2], _status.IsStepEnabled(SetupStatus.IgnoreFilesStep));
            for (var number = SetupStatus.FirstExtensionStep; number < _status.FirstSaveStep; number++)
            {
                RenderExtensionStep(_steps[number - 1], _status.GetExtensionStep(number), _status.IsStepEnabled(number));
            }
            RenderFirstSaveStep(FirstSaveElement, _status.IsStepEnabled(_status.FirstSaveStep));
            RenderRemoteStep();

            var showFinish = _status.IsComplete && (!_session.Project.SetupCompleted || _offerRemote);
            _finishRow.EnableInClassList(HiddenClass, !showFinish);
            _finishMessage.text = L10n.Tr("wizard.finish.message");

            SetEnabled(!_busy);
        }

        /// <summary>
        /// 送信先（任意）: usable once the first save exists (a repository is needed to hold the setting).
        /// Never blocks 完了; the panel is kept across renders so a typed URL is not lost.
        /// </summary>
        private void RenderRemoteStep()
        {
            var done = _status.RemoteUrl != null;
            var enabled = _status.FirstSaveDone;
            _remoteStep.SetState(done, enabled, _offerRemote && !done, L10n.Tr("step.remote.optional"));
            _remoteStep.ClearControls();
            if (done)
            {
                _remoteStep.SetMessage(L10n.Tr("step.remote.done", _status.RemoteUrl));
                _remoteStep.SetDetail(string.Empty);
                return;
            }
            _remoteStep.SetMessage(L10n.Tr("step.remote.explain"));
            _remoteStep.SetDetail(string.Empty);
            if (!enabled) return;
            if (_remotePanel == null)
            {
                _remotePanel = new RemoteSetupPanel(_session);
                _remotePanel.Changed += OnRemoteChanged;
            }
            _remoteStep.Controls.Add(_remotePanel);
        }

        private async void OnRemoteChanged()
        {
            try
            {
                _status.RemoteUrl = _session.Repository == null ? null : await _session.Repository.GetRemoteUrlAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _remoteStep.SetError(Describe(ex));
            }
            Render();
        }

        private void RenderGitStep(StepElement step, bool enabled)
        {
            var git = _status.Git;
            if (git == null)
            {
                step.SetMessage(L10n.Tr("status.checking"));
                return;
            }

            if (!git.Found)
            {
                step.SetMessage(L10n.Tr("step1.notfound"));
                step.SetDetail(string.Empty);
                step.AddButtons(enabled, (L10n.Tr("step1.download"), OpenDownloadPage), (L10n.Tr("step1.recheck"), Recheck));
                return;
            }

            var lfs = git.HasLfs ? L10n.Tr("step1.lfs.found", git.LfsVersionText) : L10n.Tr("step1.lfs.missing");
            if (!git.MeetsMinimumVersion)
            {
                step.SetMessage(L10n.Tr("step1.tooold", git.Version));
                step.SetDetail(L10n.Tr("step1.path", git.Path) + "\n" + lfs);
                step.AddButtons(enabled, (L10n.Tr("step1.download"), OpenDownloadPage), (L10n.Tr("step1.recheck"), Recheck));
                return;
            }

            step.SetMessage(L10n.Tr("step1.found", git.Version));
            step.SetDetail(L10n.Tr("step1.path", git.Path) + "\n" + lfs);
        }

        private void RenderProjectSettingsStep(StepElement step, bool enabled)
        {
            if (_status.ProjectSettingsOk)
            {
                step.SetMessage(L10n.Tr("step2.ok"));
                step.SetDetail(string.Empty);
                return;
            }
            step.SetMessage(L10n.Tr("step2.explain"));
            step.SetDetail(L10n.Tr("step2.current", UnityProjectSettings.SerializationModeName, UnityProjectSettings.VersionControlModeName));
            step.AddButtons(enabled, (L10n.Tr("step2.apply"), ApplyProjectSettings));
        }

        private void RenderIgnoreFilesStep(StepElement step, bool enabled)
        {
            if (_status.IgnoreFilesOk)
            {
                step.SetMessage(L10n.Tr("step3.ok"));
                step.SetDetail(string.Empty);
                return;
            }
            step.SetMessage(L10n.Tr("step3.explain"));
            step.SetDetail(string.Empty);
            step.AddButtons(enabled, (L10n.Tr("step3.apply"), WriteIgnoreFiles));
        }

        /// <summary>An extension step is drawn from its <see cref="SetupStepView"/>; the extension never touches the UI.</summary>
        private void RenderExtensionStep(StepElement step, ExtensionStepStatus status, bool enabled)
        {
            if (status.Error != null)
            {
                step.SetMessage(L10n.Tr("step.ext.error", status.Error.Message));
                step.SetDetail(string.Empty);
                return;
            }

            var view = status.View;
            step.SetMessage(view.Message);
            step.SetDetail(view.Detail);
            if (view.Actions.Count == 0) return;

            var buttons = new (string, Action)[view.Actions.Count];
            for (var i = 0; i < view.Actions.Count; i++)
            {
                var action = view.Actions[i];
                buttons[i] = (action.Label, () => RunExtensionAction(step, status, action));
            }
            step.AddButtons(enabled, buttons);
        }

        private void RenderFirstSaveStep(StepElement step, bool enabled)
        {
            _nameField = null;
            _emailField = null;

            if (_status.RootMismatch)
            {
                step.SetMessage(L10n.Tr("step4.mismatch", _status.Probe.TopLevel));
                step.SetDetail(string.Empty);
                return;
            }
            if (_status.FirstSaveDone)
            {
                step.SetMessage(L10n.Tr("step4.done"));
                step.SetDetail(string.Empty);
                return;
            }

            step.SetMessage(L10n.Tr("step4.explain"));
            if (enabled && _status.NeedsIdentity)
            {
                step.SetDetail(L10n.Tr("step4.identity"));
                _nameField = new TextField(L10n.Tr("step4.name"));
                _emailField = new TextField(L10n.Tr("step4.email"));
                _nameField.AddToClassList("shiori-identity-field");
                _emailField.AddToClassList("shiori-identity-field");
                step.Controls.Add(_nameField);
                step.Controls.Add(_emailField);
            }
            else
            {
                step.SetDetail(string.Empty);
            }
            step.AddButtons(enabled, (L10n.Tr("step4.apply"), FirstSave));
        }

        // ---- actions ----

        private static void OpenDownloadPage()
        {
            Application.OpenURL(ShioriSession.GitDownloadUrl);
        }

        private async void Recheck()
        {
            if (_busy) return;
            _busy = true;
            SetEnabled(false);
            try
            {
                _status = await _session.EvaluateSetupAsync(CancellationToken.None, refresh: true);
            }
            catch (Exception ex)
            {
                _steps[0].SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
                Render();
            }
        }

        private void ApplyProjectSettings()
        {
            try
            {
                UnityProjectSettings.Apply();
                _status.ProjectSettingsOk = UnityProjectSettings.IsConfigured;
            }
            catch (Exception ex)
            {
                _steps[1].SetError(Describe(ex));
            }
            Render();
        }

        private void WriteIgnoreFiles()
        {
            try
            {
                _session.WriteIgnoreFiles();
                _status.IgnoreFilesOk = _session.AreIgnoreFilesWritten();
            }
            catch (Exception ex)
            {
                _steps[2].SetError(Describe(ex));
            }
            Render();
        }

        /// <summary>Runs one extension button, then re-evaluates that step so the wizard shows the new state.</summary>
        private async void RunExtensionAction(StepElement step, ExtensionStepStatus status, SetupStepAction action)
        {
            if (_busy) return;
            _busy = true;
            SetEnabled(false);
            step.SetError(string.Empty);
            try
            {
                await action.Run(CancellationToken.None);
            }
            catch (Exception ex)
            {
                step.SetError(Describe(ex));
            }
            finally
            {
                await ShioriSession.EvaluateExtensionStepAsync(status, CancellationToken.None);
                _busy = false;
                Render();
            }
        }

        private async void FirstSave()
        {
            if (_busy) return;
            var step = FirstSaveElement;
            step.SetError(string.Empty);

            var repository = _session.Repository;
            if (repository == null) return;

            string name = null, email = null;
            if (_status.NeedsIdentity)
            {
                name = _nameField != null ? _nameField.value.Trim() : string.Empty;
                email = _emailField != null ? _emailField.value.Trim() : string.Empty;
                if (name.Length == 0 || email.Length == 0)
                {
                    step.SetError(L10n.Tr("step4.identity.required"));
                    return;
                }
            }

            _busy = true;
            SetEnabled(false);
            try
            {
                using (GitActivity.Begin(L10n.Tr("step4.progress")))
                {
                    await _session.FirstSaveAsync(name, email, CancellationToken.None);
                }
                _status = await _session.EvaluateSetupAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                step.SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
                Render();
            }

            if (!_status.IsComplete) return;
            // Offer the optional 送信先 step once before closing; 完了 skips it.
            if (_status.RemoteUrl == null)
            {
                _offerRemote = true;
                Render();
                return;
            }
            Finish();
        }

        private void Finish()
        {
            try
            {
                _session.Project.SetupCompleted = true;
                _session.SaveProjectSettings();
            }
            catch (Exception ex)
            {
                FirstSaveElement.SetError(Describe(ex));
                return;
            }
            Completed?.Invoke();
        }

        internal static string Describe(Exception ex)
        {
            if (ex is GitException git) return L10n.Tr("error.git", git.ExitCode, git.Stderr.Trim());
            return L10n.Tr("error.generic", ex.Message);
        }

        /// <summary>One rendered step: header (number, title, state) and body (message, detail, controls, error).</summary>
        private sealed class StepElement
        {
            private const string CurrentClass = "shiori-step--current";
            private const string DoneClass = "shiori-step--done";
            private const string DisabledClass = "shiori-step--disabled";

            private readonly Label _state;
            private readonly Label _message;
            private readonly Label _detail;
            private readonly Label _error;

            public VisualElement Root { get; }
            public VisualElement Controls { get; }

            public StepElement(VisualTreeAsset template, int number, string title)
            {
                Root = template.Instantiate();
                Root.Q<Label>("step-number").text = number.ToString();
                Root.Q<Label>("step-title").text = title;
                _state = Root.Q<Label>("step-state");
                _message = Root.Q<Label>("step-message");
                _detail = Root.Q<Label>("step-detail");
                _error = Root.Q<Label>("step-error");
                Controls = Root.Q<VisualElement>("step-controls");
                SetError(string.Empty);
            }

            public void SetState(bool done, bool enabled, bool current, string pendingText = null)
            {
                var card = Root.Q<VisualElement>(className: "shiori-step");
                card.EnableInClassList(DoneClass, done);
                card.EnableInClassList(CurrentClass, current && !done);
                card.EnableInClassList(DisabledClass, !enabled && !done);
                _state.text = done ? L10n.Tr("step.state.done")
                    : pendingText ?? (enabled ? L10n.Tr("step.state.todo") : L10n.Tr("step.state.blocked"));
            }

            public void SetMessage(string text)
            {
                _message.text = text ?? string.Empty;
            }

            public void SetDetail(string text)
            {
                _detail.text = text ?? string.Empty;
                _detail.EnableInClassList(HiddenClass, string.IsNullOrEmpty(text));
            }

            public void SetError(string text)
            {
                _error.text = text ?? string.Empty;
                _error.EnableInClassList(HiddenClass, string.IsNullOrEmpty(text));
            }

            public void ClearControls()
            {
                Controls.Clear();
            }

            public void AddButtons(bool enabled, params (string text, Action onClick)[] buttons)
            {
                var row = new VisualElement();
                row.AddToClassList("shiori-step-controls-row");
                foreach (var (text, onClick) in buttons)
                {
                    var button = new Button(onClick) { text = text };
                    button.SetEnabled(enabled);
                    row.Add(button);
                }
                Controls.Add(row);
            }
        }
    }
}
