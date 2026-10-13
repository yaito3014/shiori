using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>
    /// Compact rendering of one extension step for the Project Settings page: title, message,
    /// detail, buttons and error. Re-evaluates the step after each action and raises <see cref="Changed"/>.
    /// The one-line form (detail mode) shows only the heading and the buttons; the explanation is the tooltip.
    /// </summary>
    internal sealed class ExtensionStepPanel : VisualElement
    {
        private const string HiddenClass = "shiori-hidden";

        private readonly ExtensionStepStatus _status;
        private readonly bool _oneLine;
        private readonly Label _title;
        private readonly Label _message;
        private readonly Label _detail;
        private readonly VisualElement _buttons;
        private readonly Label _error;
        private bool _busy;

        public event Action Changed;

        public ExtensionStepPanel(ExtensionStepStatus status, bool oneLine = false)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _oneLine = oneLine;
            AddToClassList("shiori-ext-step");
            EnableInClassList("shiori-ext-step-line", oneLine);

            _title = new Label(status.Title);
            _title.AddToClassList("shiori-settings-section");
            Add(_title);
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
            _error.AddToClassList(HiddenClass);
            Add(_error);

            Render();
        }

        private void Render()
        {
            _buttons.Clear();
            // Notices may come without a heading; the one-line form then shows the message instead.
            var hasTitle = !string.IsNullOrEmpty(_status.Title) && !(_status.Step is NoticeStep && string.IsNullOrEmpty(_status.Step.Title));
            _title.EnableInClassList(HiddenClass, !hasTitle);
            if (_status.Error != null)
            {
                _message.text = L10n.Tr("step.ext.error", _status.Error.Message);
                _message.EnableInClassList(HiddenClass, false);
                _detail.EnableInClassList(HiddenClass, true);
                return;
            }

            var view = _status.View;
            _message.text = view.Message;
            _detail.text = view.Detail;
            _detail.EnableInClassList(HiddenClass, _oneLine || string.IsNullOrEmpty(view.Detail));
            _message.EnableInClassList(HiddenClass, _oneLine && hasTitle);
            if (_oneLine)
            {
                tooltip = string.IsNullOrEmpty(view.Detail) ? view.Message : view.Message + "\n\n" + view.Detail;
            }
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

        /// <summary>
        /// Fills <paramref name="container"/> with every pending step and current notice. Steps come first: a step
        /// is something to set up, a notice reports state. <paramref name="changed"/> runs after a button ran.
        /// </summary>
        public static async Task FillAsync(ShioriSession session, VisualElement container, bool oneLine, Action changed)
        {
            if (session.Extensions.Count == 0) return;
            var steps = await session.EvaluateExtensionStepsAsync(CancellationToken.None);
            steps.AddRange(await session.GetNoticesAsync(CancellationToken.None));
            container.Clear();
            foreach (var step in steps)
            {
                if (step.Done) continue;
                var notice = new ExtensionStepPanel(step, oneLine);
                notice.AddToClassList("shiori-notice");
                notice.AddToClassList("shiori-ext-notice");
                notice.Changed += changed;
                container.Add(notice);
            }
            container.EnableInClassList(HiddenClass, container.childCount == 0);
        }

        private void SetError(string text)
        {
            _error.text = text ?? string.Empty;
            _error.EnableInClassList(HiddenClass, string.IsNullOrEmpty(text));
        }
    }
}
