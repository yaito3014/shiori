using System;
using System.Threading;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>
    /// Sets the 送信先: paste a URL, 「確認する」 checks it without writing anything, 「この送信先にする」
    /// stores it in .git/config. Used by the setup wizard and by Project Settings > Shiori.
    /// </summary>
    internal sealed class RemoteSetupPanel : VisualElement
    {
        private const string HiddenClass = "shiori-hidden";

        private readonly ShioriSession _session;
        private readonly TextField _url;
        private readonly Button _check;
        private readonly Button _apply;
        private readonly Label _result;
        private readonly Label _error;
        private bool _busy;

        /// <summary>Raised after the 送信先 was stored.</summary>
        public event Action Changed;

        public RemoteSetupPanel(ShioriSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            AddToClassList("shiori-remote-setup");

            var help = new Label(L10n.Tr("remote.help"));
            help.AddToClassList("shiori-settings-help");
            Add(help);

            _url = new TextField(L10n.Tr("remote.url")) { name = "remote-url" };
            _url.AddToClassList("shiori-settings-field");
            Add(_url);

            var row = new VisualElement();
            row.AddToClassList("shiori-step-controls-row");
            _check = new Button(Check) { text = L10n.Tr("remote.check"), name = "remote-check" };
            _apply = new Button(Apply) { text = L10n.Tr("remote.apply"), name = "remote-apply" };
            row.Add(_check);
            row.Add(_apply);
            Add(row);

            _result = new Label { name = "remote-result" };
            _result.AddToClassList("shiori-settings-help");
            Add(_result);
            _error = new Label { name = "remote-error" };
            _error.AddToClassList("shiori-error");
            Add(_error);
            SetError(null);

            LoadCurrent();
        }

        /// <summary>The settings page builds its own session; git may not have been located yet.</summary>
        private async System.Threading.Tasks.Task<IGitRepository> RepositoryAsync()
        {
            if (_session.Repository == null) await _session.LocateGitAsync(CancellationToken.None);
            return _session.Repository;
        }

        private async void LoadCurrent()
        {
            try
            {
                var repository = await RepositoryAsync();
                if (repository == null) return;
                var current = await repository.GetRemoteUrlAsync(CancellationToken.None);
                if (current == null) return;
                _url.SetValueWithoutNotify(current);
                _result.text = L10n.Tr("remote.current", current);
            }
            catch (Exception ex)
            {
                SetError(RemoteText.DescribeAny(ex));
            }
        }

        private async void Check()
        {
            var url = _url.value?.Trim();
            if (_busy) return;
            var repository = await RepositoryAsync();
            if (repository == null) return;
            if (string.IsNullOrEmpty(url))
            {
                SetError(L10n.Tr("remote.required"));
                return;
            }
            SetBusy(true);
            SetError(null);
            _result.text = L10n.Tr("remote.checking");
            try
            {
                var state = await RemoteChecker.CheckAsync(repository, url, CancellationToken.None);
                _result.text = RemoteText.Describe(state);
            }
            catch (Exception ex)
            {
                _result.text = string.Empty;
                SetError(RemoteText.DescribeAny(ex));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Apply()
        {
            var url = _url.value?.Trim();
            if (_busy) return;
            var repository = await RepositoryAsync();
            if (repository == null) return;
            if (string.IsNullOrEmpty(url))
            {
                SetError(L10n.Tr("remote.required"));
                return;
            }
            SetBusy(true);
            SetError(null);
            try
            {
                await repository.SetRemoteUrlAsync(url, CancellationToken.None);
                _result.text = L10n.Tr("remote.saved") + "\n" + L10n.Tr("remote.current", url);
            }
            catch (Exception ex)
            {
                SetError(RemoteText.DescribeAny(ex));
                return;
            }
            finally
            {
                SetBusy(false);
            }
            Changed?.Invoke();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _check.SetEnabled(!busy);
            _apply.SetEnabled(!busy);
            _url.SetEnabled(!busy);
        }

        private void SetError(string text)
        {
            _error.text = text ?? string.Empty;
            _error.EnableInClassList(HiddenClass, string.IsNullOrEmpty(text));
        }
    }
}
