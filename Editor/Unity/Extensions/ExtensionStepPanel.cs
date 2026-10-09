using System;
using System.Threading;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>
    /// Compact rendering of one extension step for the Project Settings page: title, message,
    /// detail, buttons and error. Re-evaluates the step after each action and raises <see cref="Changed"/>.
    /// </summary>
    internal sealed class ExtensionStepPanel : VisualElement
    {
        private const string HiddenClass = "shiori-hidden";

        private readonly ExtensionStepStatus _status;
        private readonly Label _message;
        private readonly Label _detail;
        private readonly VisualElement _buttons;
        private readonly Label _error;
        private bool _busy;

        public event Action Changed;

        public ExtensionStepPanel(ExtensionStepStatus status)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            AddToClassList("shiori-ext-step");

            var title = new Label(status.Title);
            title.AddToClassList("shiori-settings-section");
            // Notices may come without a heading.
            title.EnableInClassList(HiddenClass, string.IsNullOrEmpty(status.Title) || status.Step is NoticeStep && string.IsNullOrEmpty(status.Step.Title));
            Add(title);
            _message = new Label();
            _message.AddToClassList("shiori-settings-help");
            Add(_message);
            _detail = new Label();
            _detail.AddToClassList("shiori-settings-help");
            Add(_detail);
            _buttons = new VisualElement();
            _buttons.AddToClassList("shiori-step-controls-row");
            Add(_buttons);
            _error = new Label();
            _error.AddToClassList("shiori-error");
            Add(_error);

            Render();
        }

        private void Render()
        {
            _buttons.Clear();
            if (_status.Error != null)
            {
                _message.text = L10n.Tr("step.ext.error", _status.Error.Message);
                _detail.EnableInClassList(HiddenClass, true);
                return;
            }

            var view = _status.View;
            _message.text = view.Message;
            _detail.text = view.Detail;
            _detail.EnableInClassList(HiddenClass, string.IsNullOrEmpty(view.Detail));
            foreach (var action in view.Actions)
            {
                var button = new Button(() => Run(action)) { text = action.Label };
                button.AddToClassList("shiori-small-button");
                button.SetEnabled(!_busy);
                _buttons.Add(button);
            }
        }

        private async void Run(SetupStepAction action)
        {
            if (_busy) return;
            _busy = true;
            SetError(null);
            Render();
            try
            {
                await action.Run(CancellationToken.None);
            }
            catch (Exception ex)
            {
                SetError(SetupWizardView.Describe(ex));
            }
            finally
            {
                await ShioriSession.EvaluateExtensionStepAsync(_status, CancellationToken.None);
                _busy = false;
                Render();
            }
            Changed?.Invoke();
        }

        private void SetError(string text)
        {
            _error.text = text ?? string.Empty;
            _error.EnableInClassList(HiddenClass, string.IsNullOrEmpty(text));
        }
    }
}
