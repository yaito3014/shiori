using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>Simple mode: 保存 (F2) and 履歴 (F3). Vocabulary is fixed; no git terms appear here.</summary>
    internal sealed class SimpleModeView : VisualElement, IShioriView
    {
        public const int PageSize = 200;

        /// <summary>
        /// Memo cap. Git has no limit, but GitHub cuts subjects at 72 characters and 戻す prefixes
        /// "Restore: " (9) to the memo it restores to, so 60 keeps every subject Shiori writes intact.
        /// </summary>
        public const int MemoMaxLength = 60;

        /// <summary>The counter appears this many characters before the cap, so the limit is never a surprise.</summary>
        public const int MemoCounterFrom = MemoMaxLength - 10;

        private const string HiddenClass = "shiori-hidden";
        private const string CounterFullClass = "shiori-memo-counter--full";

        private readonly ShioriSession _session;
        private readonly IGitRepository _repo;

        private readonly Label _error;
        private readonly Label _notice;
        private readonly VisualElement _extensionNotices;
        private readonly TextField _message;
        private readonly Label _placeholder;
        private readonly Label _memoCounter;
        private readonly Button _saveButton;
        private readonly Label _saveStatus;
        private readonly ListView _saveChanges;
        private readonly Foldout _metaWarnings;
        private readonly ScrollView _metaList;
        private readonly Label _historyEmpty;
        private readonly ListView _historyList;
        private readonly Button _historyMore;
        private readonly Label _detailMessage;
        private readonly Button _restoreButton;
        private readonly Label _detailMeta;
        private readonly Label _detailEmpty;
        private readonly ListView _detailFiles;

        private readonly List<Snapshot> _snapshots = new List<Snapshot>();
        private readonly List<ChangeRow> _rows = new List<ChangeRow>();
        private readonly List<ChangeRow> _pendingRows = new List<ChangeRow>();
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
            set
            {
                // maxLength only guards typing; a draft restored after a domain reload is cut here.
                var text = value ?? string.Empty;
                if (text.Length > MemoMaxLength) text = text.Substring(0, MemoMaxLength);
                _message.SetValueWithoutNotify(text);
                UpdatePlaceholder();
                UpdateMemoCounter();
            }
        }

        public SimpleModeView(ShioriSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _repo = session.Repository ?? throw new InvalidOperationException("git repository is not available");

            UiAssets.Tree("SimpleModeView.uxml").CloneTree(this);
            style.flexGrow = 1;

            _error = this.Q<Label>("view-error");
            _notice = this.Q<Label>("view-notice");
            _extensionNotices = this.Q<VisualElement>("ext-notices");
            this.Q<Label>("save-title").text = L10n.Tr("simple.save.title");
            _message = this.Q<TextField>("save-message");
            _message.label = L10n.Tr("simple.save.message.label");
            _message.maxLength = MemoMaxLength;
            _memoCounter = this.Q<Label>("memo-counter");
            _message.RegisterValueChangedCallback(e =>
            {
                DraftChanged?.Invoke(e.newValue);
                UpdatePlaceholder();
                UpdateMemoCounter();
            });
            // Enter in the memo saves, like a chat box. Registered on the field so it also catches the
            // key while the inner input has focus; TrickleDown runs before the text editor sees it.
            _message.RegisterCallback<KeyDownEvent>(OnMemoKeyDown, TrickleDown.TrickleDown);
            // UI Toolkit in 2022.3 has no placeholder; a label laid over the empty input does the job.
            _placeholder = new Label { name = "save-placeholder", pickingMode = PickingMode.Ignore };
            _placeholder.AddToClassList("shiori-placeholder");
            _placeholder.text = _session.GetMemoPlaceholder() ?? string.Empty;
            (_message.Q(TextField.textInputUssName) ?? (VisualElement)_message).Add(_placeholder);
            _message.RegisterCallback<FocusInEvent>(_ => UpdatePlaceholder(focused: true));
            _message.RegisterCallback<FocusOutEvent>(_ => UpdatePlaceholder(focused: false));
            UpdatePlaceholder();
            UpdateMemoCounter();
            _saveButton = this.Q<Button>("save-button");
            _saveButton.text = L10n.Tr("simple.save.button");
            _saveButton.tooltip = L10n.Tr("simple.save.tooltip");
            _saveButton.clicked += Save;
            _saveStatus = this.Q<Label>("save-status");
            _saveChanges = this.Q<ListView>("save-changes");
            _saveChanges.makeItem = MakeFileItem;
            _saveChanges.bindItem = (element, index) => BindFileItem(element, _pendingRows, index);
            _saveChanges.fixedItemHeight = 20;
            _saveChanges.selectionType = SelectionType.Single;
            _saveChanges.itemsSource = _pendingRows;
            _saveChanges.selectionChanged += selection => RevealSelectedRow(selection);
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
            _restoreButton = this.Q<Button>("detail-restore");
            _restoreButton.text = L10n.Tr("restore.button");
            _restoreButton.clicked += Restore;
            _detailMeta = this.Q<Label>("detail-meta");
            _detailEmpty = this.Q<Label>("detail-empty");
            _detailFiles = this.Q<ListView>("detail-files");
            _detailFiles.makeItem = MakeFileItem;
            _detailFiles.bindItem = (element, index) => BindFileItem(element, _rows, index);
            _detailFiles.fixedItemHeight = 20;
            _detailFiles.selectionType = SelectionType.Single;
            _detailFiles.itemsSource = _rows;
            _detailFiles.selectionChanged += selection => RevealSelectedRow(selection);

            SetError(null);
            SetNotice(null);
            RenderSaveStatus();
            ShowDetail(null);

            // Until the first load completes, say so instead of pretending the project is empty.
            _saveStatus.text = L10n.Tr("status.loading");
            _historyEmpty.text = L10n.Tr("status.loading");
            _historyEmpty.EnableInClassList(HiddenClass, false);
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

                SetError(null);
                RenderSaveStatus();
                RenderHistory();

                // The meta walk can take a while on big projects; show everything else first.
                var meta = await CheckMetaAsync();
                RenderMeta(meta);
                await RenderExtensionNoticesAsync();
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
                SetError(null);
                RenderSaveStatus();
                var meta = await CheckMetaAsync();
                RenderMeta(meta);
                await RenderExtensionNoticesAsync();
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

        private void OnMemoKeyDown(KeyDownEvent e)
        {
            if (ShouldSaveOnKey(e.keyCode, IsImeComposing(), _saveButton.enabledSelf && _saveButton.enabledInHierarchy))
            {
                e.StopPropagation();
#if !UNITY_6000_0_OR_NEWER
                e.PreventDefault();
#endif
                Save();
                return;
            }

            // maxLength drops the character silently; a beep says "full" the way the OS does.
            var hasSelection = _message.cursorIndex != _message.selectIndex;
            if (IsRejectedByCap(e.character, _message.value.Length, hasSelection)) EditorApplication.Beep();
        }

        /// <summary>
        /// A printable character typed into a full memo with nothing selected is the keystroke maxLength
        /// will reject. Control keys, and typing over a selection, still work and stay silent.
        /// </summary>
        internal static bool IsRejectedByCap(char character, int length, bool hasSelection)
        {
            if (length < MemoMaxLength || hasSelection) return false;
            return character >= ' ' && !char.IsControl(character);
        }

        /// <summary>"52/60" from ten characters before the cap; red at the cap. Hidden for short memos.</summary>
        private void UpdateMemoCounter()
        {
            var length = _message.value.Length;
            var show = length >= MemoCounterFrom;
            _memoCounter.text = show ? length + "/" + MemoMaxLength : string.Empty;
            _memoCounter.EnableInClassList(HiddenClass, !show);
            _memoCounter.EnableInClassList(CounterFullClass, length >= MemoMaxLength);
        }

        /// <summary>
        /// Enter saves only when nothing else wants it: the IME is not mid-conversion (Japanese input
        /// confirms a conversion with the same key) and the 保存 button itself is enabled.
        /// </summary>
        internal static bool ShouldSaveOnKey(KeyCode key, bool imeComposing, bool saveEnabled)
        {
            if (key != KeyCode.Return && key != KeyCode.KeypadEnter) return false;
            if (imeComposing) return false;
            return saveEnabled;
        }

        /// <summary>
        /// The only public view of the IME state. Reading <see cref="Input"/> throws in projects that
        /// switched to the Input System package alone; then the guard is simply off.
        /// </summary>
        private static bool IsImeComposing()
        {
            try
            {
                return !string.IsNullOrEmpty(Input.compositionString);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private async void Save()
        {
            if (_busy || _status == null) return;
            // Inspector edits are not on disk yet; git must see what the user sees.
            UnitySaver.SaveForSnapshot();
            _busy = true;
            SetNotice(null);
            _saveButton.SetEnabled(false);
            try
            {
                using (GitActivity.Begin(L10n.Tr("simple.save.progress")))
                {
                    var ct = CancellationToken.None;
                    await _session.RunBeforeSaveAsync(ct);
                    await _repo.AddAllAsync(ct);
                    var staged = await _repo.GetStatusAsync(ct);
                    string hash = null;
                    if (staged.HasChanges)
                    {
                        var message = SnapshotMessage.Resolve(_message.value, staged.Stats);
                        hash = await _repo.CommitAsync(message, ct);
                        _message.value = string.Empty;
                    }
                    else
                    {
                        SetNotice(L10n.Tr("simple.save.nochanges"));
                    }
                    await _session.RunAfterSaveAsync(hash, ct);
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

        private const int PendingRowHeight = 20;
        private const int PendingRowsVisible = 7;

        private bool _messageFocused;

        /// <summary>The placeholder shows only while the memo is empty and not being edited.</summary>
        private void UpdatePlaceholder(bool? focused = null)
        {
            if (focused.HasValue) _messageFocused = focused.Value;
            var show = !_messageFocused && string.IsNullOrEmpty(_message.value) && !string.IsNullOrEmpty(_placeholder.text);
            _placeholder.EnableInClassList(HiddenClass, !show);
        }

        /// <summary>
        /// An extension step that is pending after setup (say, VCC added a package) is shown here as a
        /// notice with the step's own button, so the user does not have to find it under Project Settings.
        /// </summary>
        private async Task RenderExtensionNoticesAsync()
        {
            if (_session.Extensions.Count == 0) return;
            var steps = await _session.EvaluateExtensionStepsAsync(CancellationToken.None);
            _extensionNotices.Clear();
            foreach (var step in steps)
            {
                if (step.Done) continue;
                var notice = new ExtensionStepPanel(step);
                notice.AddToClassList("shiori-notice");
                notice.AddToClassList("shiori-ext-notice");
                notice.Changed += OnExtensionNoticeChanged;
                _extensionNotices.Add(notice);
            }
            _extensionNotices.EnableInClassList(HiddenClass, _extensionNotices.childCount == 0);
        }

        /// <summary>After a notice's button ran, re-evaluate: a fixed step disappears, and the working tree may have changed.</summary>
        private void OnExtensionNoticeChanged()
        {
            RefreshStatus();
        }

        private void RenderSaveStatus()
        {
            var stats = _status?.Stats;
            var hasChanges = _status != null && _status.HasChanges;
            // Stay enabled even when git sees nothing: unsaved Inspector edits only reach disk when 保存 runs.
            _saveButton.SetEnabled(_status != null);
            _saveStatus.text = hasChanges
                ? L10n.Tr("simple.save.changes", stats.Total, stats.Added, stats.Modified, stats.Deleted)
                : L10n.Tr("simple.save.nochanges");

            _pendingRows.Clear();
            if (hasChanges) _pendingRows.AddRange(ChangeRowBuilder.Build(_status.Changes));
            _saveChanges.EnableInClassList(HiddenClass, _pendingRows.Count == 0);
            // A ListView needs a definite height; show up to a few rows and scroll beyond that.
            _saveChanges.style.height = Math.Min(_pendingRows.Count, PendingRowsVisible) * PendingRowHeight + 4;
            _saveChanges.RefreshItems();

            UpdateRestoreButton();
        }

        /// <summary>Restoring the current snapshot with nothing pending would change nothing, so the button is disabled then.</summary>
        private void UpdateRestoreButton()
        {
            var selected = SelectedHash;
            var isCurrentAndClean = selected != null && selected == _head && _status != null && !_status.HasChanges;
            _restoreButton.SetEnabled(selected != null && !isCurrentAndClean);
            _restoreButton.tooltip = isCurrentAndClean ? L10n.Tr("restore.nothing") : L10n.Tr("restore.tooltip");
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

        // ---- 戻す ----

        private async void Restore()
        {
            if (_busy) return;
            var index = SelectedHash == null ? -1 : _snapshots.FindIndex(s => s.Hash == SelectedHash);
            if (index < 0) return;
            var target = _snapshots[index];

            // In-memory edits are invisible to git and would be written over the restored files later;
            // flush them first (scenes with a prompt, assets silently).
            if (!UnitySaver.SaveForRestoreOrCancel()) return;

            _busy = true;
            SetNotice(null);
            try
            {
                var ct = CancellationToken.None;
                _status = await _repo.GetStatusAsync(ct);
                RenderSaveStatus();

                var mode = AskRestoreMode(target, _status.HasChanges, _session.GetRestoreWarning(target));
                if (mode == null) return;

                RestoreResult result;
                using (GitActivity.Begin(L10n.Tr("restore.progress")))
                {
                    result = await RestoreOperation.RunAsync(_repo, target.Hash, target.Message, mode.Value, _message.value, ct);
                    await _session.RunAfterRestoreAsync(result, ct);
                }
                if (mode.Value == RestoreMode.SaveFirst && result.SavedCommitHash != null) _message.value = string.Empty;

                SetError(null);
                SetNotice(DescribeRestore(target, result));
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

        /// <summary>The F4 confirmation. Extension warnings (if any) follow the main text. Returns null when the user cancels.</summary>
        private static RestoreMode? AskRestoreMode(Snapshot target, bool hasChanges, string warning)
        {
            var title = L10n.Tr("restore.dialog.title");
            var when = RelativeTime.Format(target.Time, DateTimeOffset.Now);
            if (hasChanges)
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    title,
                    WithWarning(L10n.Tr("restore.dialog.dirty", target.Message, when), warning),
                    L10n.Tr("restore.dialog.savefirst"),
                    L10n.Tr("restore.dialog.cancel"),
                    L10n.Tr("restore.dialog.discard"));
                switch (choice)
                {
                    case 0: return RestoreMode.SaveFirst;
                    case 2: return RestoreMode.StashFirst;
                    default: return null;
                }
            }

            var ok = EditorUtility.DisplayDialog(
                title,
                WithWarning(L10n.Tr("restore.dialog.clean", target.Message, when), warning),
                L10n.Tr("restore.dialog.ok"),
                L10n.Tr("restore.dialog.cancel"));
            return ok ? RestoreMode.StashFirst : (RestoreMode?)null;
        }

        internal static string WithWarning(string text, string warning)
        {
            return string.IsNullOrEmpty(warning) ? text : text + "\n\n" + warning;
        }

        private static string DescribeRestore(Snapshot target, RestoreResult result)
        {
            var sb = new StringBuilder();
            sb.Append(result.ChangedAnything ? L10n.Tr("restore.done", target.Message) : L10n.Tr("restore.nochange", target.Message));
            if (result.StashHash != null) sb.Append('\n').Append(L10n.Tr("restore.stashed"));
            if (result.TouchedProjectSettings) sb.Append('\n').Append(L10n.Tr("restore.projectsettings"));
            return sb.ToString();
        }

        // ---- 履歴 ----

        private void RenderHistory()
        {
            _historyEmpty.text = L10n.Tr("simple.history.empty");
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

            _restoreButton.EnableInClassList(HiddenClass, snapshot == null);
            UpdateRestoreButton();

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

        /// <summary>Highlights the asset in the Project window; nothing happens for deleted files or settings.</summary>
        private static void RevealSelectedRow(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is ChangeRow row) AssetNavigator.Reveal(row.Path);
                break;
            }
        }

        private static void BindFileItem(VisualElement element, List<ChangeRow> rows, int index)
        {
            if (index < 0 || index >= rows.Count) return;
            var row = rows[index];
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

        private void SetNotice(string text)
        {
            _notice.text = text ?? string.Empty;
            _notice.EnableInClassList(HiddenClass, string.IsNullOrEmpty(text));
        }

        private static string Describe(Exception ex)
        {
            if (ex is GitException git) return L10n.Tr("error.git", git.ExitCode, git.Stderr.Trim());
            return L10n.Tr("error.generic", ex.Message);
        }
    }
}
