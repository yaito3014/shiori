using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>Simple mode: 保存 (F2) and 履歴 (F3). Vocabulary is fixed; no git terms appear here.</summary>
    internal sealed class SimpleModeView : VisualElement
    {
        public const int PageSize = 200;
        private const string HiddenClass = "shiori-hidden";

        private readonly ShioriSession _session;
        private readonly IGitRepository _repo;

        private readonly Label _error;
        private readonly TextField _message;
        private readonly Button _saveButton;
        private readonly Label _saveStatus;
        private readonly Foldout _metaWarnings;
        private readonly ScrollView _metaList;
        private readonly Label _historyEmpty;
        private readonly ListView _historyList;
        private readonly Button _historyMore;
        private readonly Label _detailMessage;
        private readonly Label _detailMeta;
        private readonly Label _detailEmpty;
        private readonly ListView _detailFiles;

        private readonly List<Snapshot> _snapshots = new List<Snapshot>();
        private readonly List<ChangeRow> _rows = new List<ChangeRow>();
        private WorktreeStatus _status;
        private string _head;
        private bool _busy;
        private bool _historyExhausted;
        private string _pendingSelection;

        /// <summary>Hash of the selected history item, or null.</summary>
        public string SelectedHash { get; private set; }

        public event Action<string> SelectionChanged;
        public event Action<string> DraftChanged;

        public string DraftMessage
        {
            get => _message.value;
            set => _message.SetValueWithoutNotify(value ?? string.Empty);
        }

        public SimpleModeView(ShioriSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _repo = session.Repository ?? throw new InvalidOperationException("git repository is not available");

            UiAssets.Tree("SimpleModeView.uxml").CloneTree(this);

            _error = this.Q<Label>("view-error");
            this.Q<Label>("save-title").text = L10n.Tr("simple.save.title");
            _message = this.Q<TextField>("save-message");
            _message.label = L10n.Tr("simple.save.message.label");
            _message.RegisterValueChangedCallback(e => DraftChanged?.Invoke(e.newValue));
            _saveButton = this.Q<Button>("save-button");
            _saveButton.text = L10n.Tr("simple.save.button");
            _saveButton.clicked += Save;
            _saveStatus = this.Q<Label>("save-status");
            _metaWarnings = this.Q<Foldout>("meta-warnings");
            _metaList = this.Q<ScrollView>("meta-list");

            this.Q<Label>("history-title").text = L10n.Tr("simple.history.title");
            var refresh = this.Q<Button>("history-refresh");
            refresh.text = L10n.Tr("simple.history.refresh");
            refresh.clicked += RefreshAll;
            _historyEmpty = this.Q<Label>("history-empty");
            _historyEmpty.text = L10n.Tr("simple.history.empty");
            _historyList = this.Q<ListView>("history-list");
            _historyList.makeItem = MakeHistoryItem;
            _historyList.bindItem = BindHistoryItem;
            _historyList.fixedItemHeight = 22;
            _historyList.selectionType = SelectionType.Single;
            _historyList.itemsSource = _snapshots;
            _historyList.selectionChanged += OnHistorySelectionChanged;
            _historyMore = this.Q<Button>("history-more");
            _historyMore.text = L10n.Tr("simple.history.more");
            _historyMore.clicked += LoadMore;

            _detailMessage = this.Q<Label>("detail-message");
            _detailMeta = this.Q<Label>("detail-meta");
            _detailEmpty = this.Q<Label>("detail-empty");
            _detailFiles = this.Q<ListView>("detail-files");
            _detailFiles.makeItem = MakeFileItem;
            _detailFiles.bindItem = BindFileItem;
            _detailFiles.fixedItemHeight = 20;
            _detailFiles.selectionType = SelectionType.None;
            _detailFiles.itemsSource = _rows;

            SetError(null);
            RenderSaveStatus();
            ShowDetail(null);
        }

        /// <summary>Selects this hash once the history has loaded (used to survive a domain reload).</summary>
        public void RestoreSelection(string hash)
        {
            _pendingSelection = hash;
        }

        // ---- refresh ----

        public async void RefreshAll()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var ct = CancellationToken.None;
                _head = await _repo.GetHeadAsync(ct);
                _status = await _repo.GetStatusAsync(ct);
                var log = await _repo.GetLogAsync(PageSize, 0, ct);
                _snapshots.Clear();
                _snapshots.AddRange(log);
                _historyExhausted = log.Count < PageSize;
                var meta = await CheckMetaAsync();

                SetError(null);
                RenderSaveStatus();
                RenderMeta(meta);
                RenderHistory();
            }
            catch (Exception ex)
            {
                SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Cheaper refresh for project-changed notifications: working tree and meta only.</summary>
        public async void RefreshStatus()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                _status = await _repo.GetStatusAsync(CancellationToken.None);
                var meta = await CheckMetaAsync();
                SetError(null);
                RenderSaveStatus();
                RenderMeta(meta);
            }
            catch (Exception ex)
            {
                SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
            }
        }

        private Task<MetaCheckResult> CheckMetaAsync()
        {
            var root = _session.ProjectRoot;
            return Task.Run(() => MetaChecker.Check(root));
        }

        private async void LoadMore()
        {
            if (_busy || _historyExhausted) return;
            _busy = true;
            try
            {
                var page = await _repo.GetLogAsync(PageSize, _snapshots.Count, CancellationToken.None);
                _snapshots.AddRange(page);
                _historyExhausted = page.Count < PageSize;
                RenderHistory();
            }
            catch (Exception ex)
            {
                SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
            }
        }

        // ---- 保存 ----

        private async void Save()
        {
            if (_busy || _status == null || !_status.HasChanges) return;
            _busy = true;
            _saveButton.SetEnabled(false);
            try
            {
                using (GitActivity.Begin(L10n.Tr("simple.save.progress")))
                {
                    var ct = CancellationToken.None;
                    await _repo.AddAllAsync(ct);
                    var staged = await _repo.GetStatusAsync(ct);
                    if (staged.HasChanges)
                    {
                        var message = SnapshotMessage.Resolve(_message.value, staged.Stats);
                        await _repo.CommitAsync(message, ct);
                        _message.value = string.Empty;
                    }
                }
                SetError(null);
            }
            catch (Exception ex)
            {
                SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
            }
            RefreshAll();
        }

        private void RenderSaveStatus()
        {
            var stats = _status?.Stats;
            var hasChanges = _status != null && _status.HasChanges;
            _saveButton.SetEnabled(hasChanges);
            _saveStatus.text = hasChanges
                ? L10n.Tr("simple.save.changes", stats.Total, stats.Added, stats.Modified, stats.Deleted)
                : L10n.Tr("simple.save.nochanges");
        }

        private void RenderMeta(MetaCheckResult meta)
        {
            _metaList.Clear();
            var issues = meta == null ? 0 : meta.MissingMeta.Count + meta.OrphanMeta.Count;
            _metaWarnings.EnableInClassList(HiddenClass, issues == 0);
            if (issues == 0) return;

            _metaWarnings.text = L10n.Tr("simple.meta.title", issues);
            foreach (var path in meta.MissingMeta) _metaList.Add(new Label(L10n.Tr("simple.meta.missing", path)));
            foreach (var path in meta.OrphanMeta) _metaList.Add(new Label(L10n.Tr("simple.meta.orphan", path)));
        }

        // ---- 履歴 ----

        private void RenderHistory()
        {
            _historyEmpty.EnableInClassList(HiddenClass, _snapshots.Count > 0);
            _historyMore.EnableInClassList(HiddenClass, _historyExhausted);
            _historyList.RefreshItems();

            var wanted = _pendingSelection ?? SelectedHash;
            _pendingSelection = null;
            var index = wanted == null ? -1 : _snapshots.FindIndex(s => s.Hash == wanted);
            if (index >= 0)
            {
                _historyList.SetSelectionWithoutNotify(new[] { index });
                ShowDetail(_snapshots[index]);
            }
            else
            {
                _historyList.ClearSelection();
                ShowDetail(null);
            }
        }

        private static VisualElement MakeHistoryItem()
        {
            var row = new VisualElement();
            row.AddToClassList("shiori-history-item");
            var time = new Label { name = "time" };
            time.AddToClassList("shiori-history-time");
            var message = new Label { name = "message" };
            message.AddToClassList("shiori-history-message");
            var current = new Label(L10n.Tr("simple.history.current")) { name = "current" };
            current.AddToClassList("shiori-history-current");
            var count = new Label { name = "count" };
            count.AddToClassList("shiori-history-count");
            row.Add(time);
            row.Add(message);
            row.Add(current);
            row.Add(count);
            return row;
        }

        private void BindHistoryItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _snapshots.Count) return;
            var snapshot = _snapshots[index];
            element.Q<Label>("time").text = RelativeTime.Format(snapshot.Time, DateTimeOffset.Now);
            element.Q<Label>("message").text = snapshot.Message;
            element.Q<Label>("message").tooltip = snapshot.Message;
            element.Q<Label>("count").text = L10n.Tr("simple.history.files", snapshot.Stats.Total);
            element.Q<Label>("current").EnableInClassList(HiddenClass, snapshot.Hash != _head);
        }

        private void OnHistorySelectionChanged(IEnumerable<object> selection)
        {
            Snapshot selected = null;
            foreach (var item in selection)
            {
                selected = item as Snapshot;
                break;
            }
            ShowDetail(selected);
            SelectionChanged?.Invoke(SelectedHash);
        }

        private void ShowDetail(Snapshot snapshot)
        {
            SelectedHash = snapshot?.Hash;
            _rows.Clear();

            if (snapshot == null)
            {
                _detailMessage.text = string.Empty;
                _detailMeta.text = string.Empty;
                _detailEmpty.text = L10n.Tr("simple.detail.empty");
                _detailEmpty.EnableInClassList(HiddenClass, false);
                _detailFiles.EnableInClassList(HiddenClass, true);
                _detailFiles.RefreshItems();
                return;
            }

            _rows.AddRange(ChangeRowBuilder.Build(snapshot.Changes));
            _detailMessage.text = snapshot.Message;
            _detailMeta.text = L10n.Tr("simple.detail.by", snapshot.Time.ToLocalTime().DateTime, snapshot.Author);
            var empty = _rows.Count == 0;
            _detailEmpty.text = L10n.Tr("simple.detail.nofiles");
            _detailEmpty.EnableInClassList(HiddenClass, !empty);
            _detailFiles.EnableInClassList(HiddenClass, empty);
            _detailFiles.RefreshItems();
        }

        private static VisualElement MakeFileItem()
        {
            var row = new VisualElement();
            row.AddToClassList("shiori-file-item");
            var kind = new Label { name = "kind" };
            kind.AddToClassList("shiori-file-kind");
            var path = new Label { name = "path" };
            path.AddToClassList("shiori-file-path");
            var meta = new Label(".meta") { name = "meta" };
            meta.AddToClassList("shiori-file-meta");
            row.Add(kind);
            row.Add(path);
            row.Add(meta);
            return row;
        }

        private void BindFileItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            var row = _rows[index];
            element.Q<Label>("kind").text = KindLabel(row.Kind);
            var path = element.Q<Label>("path");
            path.text = row.MetaOnly ? L10n.Tr("row.metaonly", row.DisplayPath) : row.DisplayPath;
            path.tooltip = row.Path;
            element.Q<Label>("meta").EnableInClassList(HiddenClass, !row.HasMeta || row.MetaOnly);
        }

        internal static string KindLabel(ChangeKind kind)
        {
            switch (kind)
            {
                case ChangeKind.Added:
                case ChangeKind.Untracked: return L10n.Tr("kind.added");
                case ChangeKind.Modified: return L10n.Tr("kind.modified");
                case ChangeKind.Deleted: return L10n.Tr("kind.deleted");
                case ChangeKind.Renamed: return L10n.Tr("kind.renamed");
                case ChangeKind.Copied: return L10n.Tr("kind.copied");
                case ChangeKind.TypeChanged: return L10n.Tr("kind.typechanged");
                case ChangeKind.Unmerged: return L10n.Tr("kind.unmerged");
                case ChangeKind.Ignored: return L10n.Tr("kind.ignored");
                default: return L10n.Tr("kind.unknown");
            }
        }

        // ---- errors ----

        private void SetError(string text)
        {
            _error.text = text ?? string.Empty;
            _error.EnableInClassList(HiddenClass, string.IsNullOrEmpty(text));
        }

        private static string Describe(Exception ex)
        {
            if (ex is GitException git) return L10n.Tr("error.git", git.ExitCode, git.Stderr.Trim());
            return L10n.Tr("error.generic", ex.Message);
        }
    }
}
