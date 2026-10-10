using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>
    /// Detail mode: the same operations as simple mode under their git names, for people who know git.
    /// A toolbar commits (stage all + commit), pushes and pulls (fast-forward only); three tabs show the
    /// working-tree changes with diffs, the commit log with "restore to this state", and every stash
    /// with apply. Every action goes through the same flows as simple mode (<see cref="SaveFlow"/>,
    /// <see cref="RestoreFlow"/>, <see cref="SendRunner"/>, <see cref="ReceiveOperation"/>,
    /// <see cref="SetAsideOperation"/>), so behaviour and safety rules are identical.
    /// </summary>
    internal sealed class DetailModeView : VisualElement, IShioriView
    {
        public const int MaxDiffLines = 4000;
        public const int LogPageSize = 200;
        private const string HiddenClass = "shiori-hidden";
        private const string TabActiveClass = "shiori-mode-button--active";

        public const string TabChanges = "changes";
        public const string TabHistory = "history";
        public const string TabStashes = "stashes";

        /// <summary>The editor's own monospace font (used by the Console). Loaded once per domain.</summary>
        internal static readonly string[] MonospaceFontPaths =
        {
            "Fonts/RobotoMono/RobotoMono-Regular.ttf",
            "Fonts/robotomono/RobotoMono-Regular.ttf",
        };

        private static Font _monospace;
        private static bool _monospaceSearched;

        private readonly ShioriSession _session;
        private readonly IGitRepository _repo;
        private readonly Label _error;
        private readonly Label _notice;

        private readonly TextField _commitMessage;
        private readonly Button _commitButton;
        private readonly Button _pushButton;
        private readonly Button _pullButton;
        private readonly Label _remoteStatus;

        private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, VisualElement> _pages = new Dictionary<string, VisualElement>();

        private readonly Label _filesEmpty;
        private readonly ListView _filesList;
        private readonly Label _diffTitle;
        private readonly Label _diffEmpty;
        private readonly Label _diffTruncated;
        private readonly ListView _diffLines;

        private readonly Label _logEmpty;
        private readonly ListView _logList;
        private readonly Button _logMore;
        private readonly Label _commitTitle;
        private readonly Label _commitMeta;
        private readonly Button _commitRestore;
        private readonly ListView _commitFiles;

        private readonly Label _stashEmpty;
        private readonly ScrollView _stashList;

        private readonly List<FileChange> _changes = new List<FileChange>();
        private readonly List<DiffLine> _lines = new List<DiffLine>();
        private readonly List<Snapshot> _log = new List<Snapshot>();
        private readonly List<FileChange> _commitChanges = new List<FileChange>();
        private readonly List<StashEntry> _stashes = new List<StashEntry>();

        private WorktreeStatus _status;
        private string _head;
        private SendStatus _sendStatus;
        private RemoteComparison _comparison;
        private bool _logExhausted;
        private bool _busy;
        private bool _refreshPending;
        private string _pendingSelection;
        private int _diffGeneration;

        public string SelectedPath { get; private set; }

        /// <summary>The selected commit in the log tab, or null.</summary>
        public Snapshot SelectedCommit { get; private set; }

        public string CurrentTab { get; private set; }

        public event Action<string> SelectionChanged;

        public DetailModeView(ShioriSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _repo = session.Repository ?? throw new InvalidOperationException("git repository is not available");

            UiAssets.Tree("DetailModeView.uxml").CloneTree(this);
            // The cloned tree sits inside this element; without this the split views collapse to zero height.
            style.flexGrow = 1;

            _error = this.Q<Label>("view-error");
            _notice = this.Q<Label>("view-notice");

            // ---- toolbar ----
            _commitMessage = this.Q<TextField>("commit-message");
            _commitMessage.label = L10n.Tr("detail.commit.label");
            _commitButton = this.Q<Button>("commit-button");
            _commitButton.text = L10n.Tr("detail.commit");
            _commitButton.tooltip = L10n.Tr("detail.commit.tooltip");
            _commitButton.clicked += Commit;
            _pushButton = this.Q<Button>("push-button");
            _pushButton.tooltip = L10n.Tr("detail.push.tooltip");
            _pushButton.clicked += Push;
            _pullButton = this.Q<Button>("pull-button");
            _pullButton.text = L10n.Tr("detail.pull");
            _pullButton.tooltip = L10n.Tr("detail.pull.tooltip");
            _pullButton.clicked += Pull;
            _remoteStatus = this.Q<Label>("remote-status");
            var refresh = this.Q<Button>("files-refresh");
            refresh.text = L10n.Tr("detail.refresh");
            refresh.clicked += RefreshAll;

            // ---- tabs ----
            AddTab(TabChanges, "tab-changes", "page-changes", "detail.tab.changes");
            AddTab(TabHistory, "tab-history", "page-history", "detail.tab.history");
            AddTab(TabStashes, "tab-stashes", "page-stashes", "detail.tab.stashes");

            // ---- changes ----
            this.Q<Label>("files-title").text = L10n.Tr("detail.files.title");
            _filesEmpty = this.Q<Label>("files-empty");
            _filesEmpty.text = L10n.Tr("detail.files.empty");
            _filesList = this.Q<ListView>("files-list");
            _filesList.makeItem = MakeFileItem;
            _filesList.bindItem = (element, index) => BindFileItem(element, _changes, index);
            _filesList.fixedItemHeight = 20;
            _filesList.selectionType = SelectionType.Single;
            _filesList.itemsSource = _changes;
            _filesList.selectionChanged += OnFileSelectionChanged;

            _diffTitle = this.Q<Label>("diff-title");
            _diffEmpty = this.Q<Label>("diff-empty");
            _diffTruncated = this.Q<Label>("diff-truncated");
            _diffLines = this.Q<ListView>("diff-lines");
            _diffLines.makeItem = MakeDiffLine;
            _diffLines.bindItem = BindDiffLine;
            _diffLines.fixedItemHeight = 18;
            _diffLines.selectionType = SelectionType.None;
            _diffLines.itemsSource = _lines;
            var monospace = Monospace;
            if (monospace != null) _diffLines.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(monospace));

            // ---- log ----
            _logEmpty = this.Q<Label>("log-empty");
            _logEmpty.text = L10n.Tr("detail.log.empty");
            _logList = this.Q<ListView>("log-list");
            _logList.makeItem = MakeLogItem;
            _logList.bindItem = BindLogItem;
            _logList.fixedItemHeight = 20;
            _logList.selectionType = SelectionType.Single;
            _logList.itemsSource = _log;
            _logList.selectionChanged += OnLogSelectionChanged;
            _logMore = this.Q<Button>("log-more");
            _logMore.text = L10n.Tr("detail.log.more");
            _logMore.clicked += LoadMoreLog;
            _commitTitle = this.Q<Label>("commit-title");
            _commitMeta = this.Q<Label>("commit-meta");
            _commitRestore = this.Q<Button>("commit-restore");
            _commitRestore.text = L10n.Tr("detail.log.restore");
            _commitRestore.tooltip = L10n.Tr("detail.log.restore.tooltip");
            _commitRestore.clicked += RestoreSelectedCommit;
            _commitFiles = this.Q<ListView>("commit-files");
            _commitFiles.makeItem = MakeFileItem;
            _commitFiles.bindItem = (element, index) => BindFileItem(element, _commitChanges, index);
            _commitFiles.fixedItemHeight = 20;
            _commitFiles.selectionType = SelectionType.Single;
            _commitFiles.itemsSource = _commitChanges;
            _commitFiles.selectionChanged += selection =>
            {
                foreach (var item in selection)
                {
                    if (item is FileChange change) AssetNavigator.Reveal(change.Path);
                    break;
                }
            };

            // ---- stashes ----
            this.Q<Label>("stash-help").text = L10n.Tr("detail.stash.help");
            _stashEmpty = this.Q<Label>("stash-empty");
            _stashEmpty.text = L10n.Tr("detail.stash.empty");
            _stashList = this.Q<ScrollView>("stash-list");

            SetError(null);
            SetNotice(null);
            ShowDiff(null, null, false);
            ShowCommit(null);
            RenderRemote();
            ShowTab(IsTab(session.User.LastTab) ? session.User.LastTab : TabChanges, remember: false);
        }

        /// <summary>
        /// A font asset bundled with the editor. OS fonts created with CreateDynamicFontFromOSFont
        /// render garbled in UI Toolkit editor windows, so they are deliberately not used.
        /// Null when the bundled font cannot be found; the default font is used then.
        /// </summary>
        internal static Font Monospace
        {
            get
            {
                if (_monospaceSearched) return _monospace;
                _monospaceSearched = true;
                foreach (var path in MonospaceFontPaths)
                {
                    _monospace = UnityEditor.EditorGUIUtility.Load(path) as Font;
                    if (_monospace != null) break;
                }
                return _monospace;
            }
        }

        public void RestoreSelection(string path)
        {
            _pendingSelection = path;
        }

        // ---- tabs ----

        private static bool IsTab(string id)
        {
            return id == TabChanges || id == TabHistory || id == TabStashes;
        }

        private void AddTab(string id, string buttonName, string pageName, string labelKey)
        {
            var button = this.Q<Button>(buttonName);
            button.text = L10n.Tr(labelKey);
            button.clicked += () => ShowTab(id, remember: true);
            _tabButtons[id] = button;
            _pages[id] = this.Q<VisualElement>(pageName);
        }

        /// <summary>Shows one tab; the choice is kept in UserSettings/Shiori.json (lastTab).</summary>
        public void ShowTab(string id, bool remember = true)
        {
            if (!IsTab(id)) id = TabChanges;
            CurrentTab = id;
            foreach (var pair in _tabButtons) pair.Value.EnableInClassList(TabActiveClass, pair.Key == id);
            foreach (var pair in _pages) pair.Value.EnableInClassList(HiddenClass, pair.Key != id);
            if (!remember || _session.User.LastTab == id) return;
            _session.User.LastTab = id;
            try
            {
                _session.SaveUserSettings();
            }
            catch (Exception ex)
            {
                // Remembering the tab is a convenience; it must not get in the way.
                Debug.LogWarning("Shiori: could not remember the tab: " + ex.Message);
            }
        }

        // ---- refresh ----

        private void RunPendingRefresh()
        {
            if (!_refreshPending || _busy) return;
            _refreshPending = false;
            RefreshAll();
        }

        public async void RefreshAll()
        {
            if (_busy)
            {
                _refreshPending = true;
                return;
            }
            _busy = true;
            try
            {
                var ct = CancellationToken.None;
                _head = await _repo.GetHeadAsync(ct);
                _status = await _repo.GetStatusAsync(ct);
                _changes.Clear();
                _changes.AddRange(_status.Changes);
                var log = await _repo.GetLogAsync(LogPageSize, 0, ct);
                _log.Clear();
                _log.AddRange(log);
                _logExhausted = log.Count < LogPageSize;
                _stashes.Clear();
                _stashes.AddRange(await _repo.StashListAsync(ct));
                _sendStatus = await SendRunner.GetStatusAsync(_repo, ct);
                _comparison = _sendStatus.HasRemote ? await ReceiveRunner.CompareAsync(_repo, ct) : null;

                SetError(null);
                RenderFiles();
                RenderLog();
                RenderStashes();
                RenderRemote();
            }
            catch (Exception ex)
            {
                SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
            }
            RunPendingRefresh();
            CheckRemoteInBackground();
        }

        /// <summary>Cheaper refresh for project-changed notifications: the working tree only.</summary>
        public async void RefreshStatus()
        {
            if (_busy)
            {
                _refreshPending = true;
                return;
            }
            _busy = true;
            try
            {
                _status = await _repo.GetStatusAsync(CancellationToken.None);
                _changes.Clear();
                _changes.AddRange(_status.Changes);
                SetError(null);
                RenderFiles();
            }
            catch (Exception ex)
            {
                SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
            }
            RunPendingRefresh();
        }

        private async void CheckRemoteInBackground()
        {
            if (_sendStatus == null || !_sendStatus.HasRemote) return;
            var comparison = await RemoteWatch.CheckIfDueAsync(_repo);
            if (comparison == null) return;
            _comparison = comparison;
            RenderRemote();
        }

        // ---- toolbar: commit / push / pull ----

        private void RenderRemote()
        {
            var hasRemote = _sendStatus != null && _sendStatus.HasRemote;
            _pushButton.text = hasRemote ? L10n.Tr("detail.push") : L10n.Tr("detail.remote.setup");
            _pullButton.EnableInClassList(HiddenClass, !hasRemote);
            _remoteStatus.text = DescribeRemote(_sendStatus, _comparison);
            _remoteStatus.tooltip = hasRemote ? _sendStatus.RemoteUrl : string.Empty;
        }

        /// <summary>"origin: ↑ahead ↓behind" from local refs, or why it is unknown.</summary>
        internal static string DescribeRemote(SendStatus status, RemoteComparison comparison)
        {
            if (status == null || !status.HasRemote) return L10n.Tr("detail.remote.none");
            if (comparison == null || !comparison.Known)
            {
                return status.NeverSent ? L10n.Tr("detail.remote.unknown") : L10n.Tr("detail.remote.status", status.Unsent, 0);
            }
            return comparison.Diverged
                ? L10n.Tr("detail.remote.diverged", comparison.Ahead, comparison.Behind)
                : L10n.Tr("detail.remote.status", comparison.Ahead, comparison.Behind);
        }

        private async void Commit()
        {
            if (_busy) return;
            _busy = true;
            SetNotice(null);
            try
            {
                string hash;
                using (GitActivity.Begin(L10n.Tr("detail.commit.progress")))
                {
                    hash = await SaveFlow.RunAsync(_session, _commitMessage.value, CancellationToken.None);
                }
                if (hash != null)
                {
                    _commitMessage.value = string.Empty;
                    SetNotice(L10n.Tr("detail.commit.done", hash.Length > 7 ? hash.Substring(0, 7) : hash));
                }
                else
                {
                    SetNotice(L10n.Tr("detail.commit.nothing"));
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

        private async void Push()
        {
            if (_busy) return;
            if (_sendStatus == null || !_sendStatus.HasRemote)
            {
                SettingsService.OpenProjectSettings(ShioriSettingsProviders.ProjectPath);
                return;
            }
            _busy = true;
            SetNotice(null);
            try
            {
                SendResult result;
                using (GitActivity.Begin(L10n.Tr("send.progress")))
                {
                    result = await SendRunner.SendAsync(_repo, CancellationToken.None);
                }
                SetError(null);
                SetNotice(DescribePush(result));
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

        internal static string DescribePush(SendResult result)
        {
            switch (result.Outcome)
            {
                case SendOutcome.Sent: return L10n.Tr("detail.push.done", result.Count);
                case SendOutcome.NothingToSend: return L10n.Tr("detail.push.nothing");
                case SendOutcome.NothingSaved: return L10n.Tr("detail.log.empty");
                default: return L10n.Tr("detail.remote.none");
            }
        }

        private async void Pull()
        {
            if (_busy || _sendStatus == null || !_sendStatus.HasRemote) return;
            // Unsaved editor edits would be overwritten by the pulled files; flush them so the check sees them.
            if (!UnitySaver.SaveForRestoreOrCancel()) return;
            _busy = true;
            SetNotice(null);
            try
            {
                ReceiveResult result;
                using (GitActivity.Begin(L10n.Tr("receive.progress")))
                {
                    result = await ReceiveOperation.RunAsync(_repo, CancellationToken.None);
                }
                RemoteWatch.MarkFetched();
                SetError(null);
                SetNotice(DescribePull(result));
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

        internal static string DescribePull(ReceiveResult result)
        {
            switch (result.Outcome)
            {
                case ReceiveOutcome.Received: return L10n.Tr("detail.pull.done", result.Count);
                case ReceiveOutcome.NothingToReceive: return L10n.Tr("detail.pull.nothing");
                case ReceiveOutcome.UnsavedChanges: return L10n.Tr("detail.pull.dirty");
                case ReceiveOutcome.Diverged: return L10n.Tr("detail.pull.diverged", result.Count);
                case ReceiveOutcome.Unrelated: return L10n.Tr("detail.pull.unrelated");
                case ReceiveOutcome.NothingSaved: return L10n.Tr("detail.log.empty");
                default: return L10n.Tr("detail.remote.none");
            }
        }

        // ---- changes tab ----

        private void RenderFiles()
        {
            _filesEmpty.EnableInClassList(HiddenClass, _changes.Count > 0);
            _filesList.RefreshItems();

            var wanted = _pendingSelection ?? SelectedPath;
            _pendingSelection = null;
            var index = wanted == null ? -1 : _changes.FindIndex(c => c.Path == wanted);
            if (index >= 0)
            {
                _filesList.SetSelectionWithoutNotify(new[] { index });
                LoadDiff(_changes[index]);
            }
            else
            {
                _filesList.ClearSelection();
                SelectedPath = null;
                ShowDiff(null, null, false);
            }
        }

        private static VisualElement MakeFileItem()
        {
            var row = new VisualElement();
            row.AddToClassList("shiori-file-item");
            var status = new Label { name = "status" };
            status.AddToClassList("shiori-file-status");
            var path = new Label { name = "path" };
            path.AddToClassList("shiori-file-path");
            row.Add(status);
            row.Add(path);
            return row;
        }

        private static void BindFileItem(VisualElement element, List<FileChange> changes, int index)
        {
            if (index < 0 || index >= changes.Count) return;
            var change = changes[index];
            element.Q<Label>("status").text = new string(new[] { change.IndexStatus, change.WorktreeStatus }).Trim();
            var path = element.Q<Label>("path");
            path.text = change.OldPath == null ? change.Path : change.OldPath + " -> " + change.Path;
            path.tooltip = change.Kind.ToString();
        }

        private void OnFileSelectionChanged(IEnumerable<object> selection)
        {
            FileChange selected = null;
            foreach (var item in selection)
            {
                selected = item as FileChange;
                break;
            }
            if (selected == null)
            {
                SelectedPath = null;
                ShowDiff(null, null, false);
            }
            else
            {
                LoadDiff(selected);
                AssetNavigator.Reveal(selected.Path);
            }
            SelectionChanged?.Invoke(SelectedPath);
        }

        private async void LoadDiff(FileChange change)
        {
            SelectedPath = change.Path;
            var generation = ++_diffGeneration;
            _diffTitle.text = change.Path;
            try
            {
                var untracked = change.Kind == ChangeKind.Untracked;
                var text = await _repo.GetDiffAsync(change.Path, untracked, CancellationToken.None);
                if (generation != _diffGeneration) return;
                var lines = WithoutHeaders(DiffParser.Parse(text));
                var truncated = lines.Count > MaxDiffLines;
                ShowDiff(change.Path, lines, truncated);
                SetError(null);
            }
            catch (Exception ex)
            {
                if (generation != _diffGeneration) return;
                SetError(Describe(ex));
            }
        }

        /// <summary>Drops "diff --git", "index", "---", "+++" and similar lines; the file name is already in the title.</summary>
        internal static List<DiffLine> WithoutHeaders(IReadOnlyList<DiffLine> lines)
        {
            var result = new List<DiffLine>(lines.Count);
            foreach (var line in lines)
            {
                if (line.Kind != DiffLineKind.Header) result.Add(line);
            }
            return result;
        }

        private void ShowDiff(string path, IReadOnlyList<DiffLine> lines, bool truncated)
        {
            _lines.Clear();
            if (path == null)
            {
                _diffTitle.text = string.Empty;
                _diffEmpty.text = L10n.Tr("detail.diff.select");
                _diffEmpty.EnableInClassList(HiddenClass, false);
                _diffTruncated.EnableInClassList(HiddenClass, true);
                _diffLines.EnableInClassList(HiddenClass, true);
                _diffLines.RefreshItems();
                return;
            }

            for (var i = 0; i < lines.Count && i < MaxDiffLines; i++) _lines.Add(lines[i]);
            var empty = _lines.Count == 0;
            _diffEmpty.text = L10n.Tr("detail.diff.none");
            _diffEmpty.EnableInClassList(HiddenClass, !empty);
            _diffTruncated.text = L10n.Tr("detail.diff.truncated", MaxDiffLines);
            _diffTruncated.EnableInClassList(HiddenClass, !truncated);
            _diffLines.EnableInClassList(HiddenClass, empty);
            _diffLines.RefreshItems();
            _diffLines.ScrollToItem(0);
        }

        private static VisualElement MakeDiffLine()
        {
            var label = new Label();
            label.AddToClassList("shiori-diff-line");
            return label;
        }

        private void BindDiffLine(VisualElement element, int index)
        {
            if (index < 0 || index >= _lines.Count) return;
            var line = _lines[index];
            var label = (Label)element;
            label.text = line.Text;
            label.EnableInClassList("shiori-diff-line--header", line.Kind == DiffLineKind.Header);
            label.EnableInClassList("shiori-diff-line--hunk", line.Kind == DiffLineKind.Hunk);
            label.EnableInClassList("shiori-diff-line--added", line.Kind == DiffLineKind.Added);
            label.EnableInClassList("shiori-diff-line--removed", line.Kind == DiffLineKind.Removed);
            label.EnableInClassList("shiori-diff-line--meta", line.Kind == DiffLineKind.Meta);
        }

        // ---- log tab ----

        private void RenderLog()
        {
            _logEmpty.EnableInClassList(HiddenClass, _log.Count > 0);
            _logMore.EnableInClassList(HiddenClass, _logExhausted);
            _logList.RefreshItems();
            var index = SelectedCommit == null ? -1 : _log.FindIndex(s => s.Hash == SelectedCommit.Hash);
            if (index >= 0)
            {
                _logList.SetSelectionWithoutNotify(new[] { index });
                ShowCommit(_log[index]);
            }
            else
            {
                _logList.ClearSelection();
                ShowCommit(null);
            }
        }

        private async void LoadMoreLog()
        {
            if (_busy || _logExhausted) return;
            _busy = true;
            try
            {
                var page = await _repo.GetLogAsync(LogPageSize, _log.Count, CancellationToken.None);
                _log.AddRange(page);
                _logExhausted = page.Count < LogPageSize;
                RenderLog();
            }
            catch (Exception ex)
            {
                SetError(Describe(ex));
            }
            finally
            {
                _busy = false;
            }
            RunPendingRefresh();
        }

        private static VisualElement MakeLogItem()
        {
            var row = new VisualElement();
            row.AddToClassList("shiori-history-item");
            var hash = new Label { name = "hash" };
            hash.AddToClassList("shiori-file-status");
            hash.AddToClassList("shiori-detail-hash");
            var time = new Label { name = "time" };
            time.AddToClassList("shiori-history-time");
            var message = new Label { name = "message" };
            message.AddToClassList("shiori-history-message");
            var head = new Label(L10n.Tr("detail.head")) { name = "head" };
            head.AddToClassList("shiori-history-current");
            row.Add(hash);
            row.Add(time);
            row.Add(message);
            row.Add(head);
            return row;
        }

        private void BindLogItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _log.Count) return;
            var snapshot = _log[index];
            element.Q<Label>("hash").text = snapshot.ShortHash;
            element.Q<Label>("time").text = RelativeTime.Format(snapshot.Time, DateTimeOffset.Now);
            element.Q<Label>("message").text = snapshot.Message;
            element.Q<Label>("message").tooltip = snapshot.Message;
            element.Q<Label>("head").EnableInClassList(HiddenClass, snapshot.Hash != _head);
        }

        private void OnLogSelectionChanged(IEnumerable<object> selection)
        {
            Snapshot selected = null;
            foreach (var item in selection)
            {
                selected = item as Snapshot;
                break;
            }
            ShowCommit(selected);
        }

        private void ShowCommit(Snapshot snapshot)
        {
            SelectedCommit = snapshot;
            _commitChanges.Clear();
            _commitRestore.EnableInClassList(HiddenClass, snapshot == null);
            if (snapshot == null)
            {
                _commitTitle.text = string.Empty;
                _commitMeta.text = L10n.Tr("detail.log.select");
                _commitFiles.RefreshItems();
                return;
            }
            _commitChanges.AddRange(snapshot.Changes);
            _commitTitle.text = snapshot.Message;
            _commitMeta.text = L10n.Tr("detail.log.meta", snapshot.ShortHash, snapshot.Time.ToLocalTime().DateTime, snapshot.Author);
            var isCurrentAndClean = snapshot.Hash == _head && _status != null && !_status.HasChanges;
            _commitRestore.SetEnabled(!isCurrentAndClean);
            _commitRestore.tooltip = isCurrentAndClean ? L10n.Tr("restore.nothing") : L10n.Tr("detail.log.restore.tooltip");
            _commitFiles.RefreshItems();
        }

        private async void RestoreSelectedCommit()
        {
            var target = SelectedCommit;
            if (_busy || target == null) return;
            _busy = true;
            SetNotice(null);
            try
            {
                var outcome = await RestoreFlow.RunAsync(_session, target, _commitMessage.value, CancellationToken.None);
                if (outcome == null) return;
                if (outcome.Mode == RestoreMode.SaveFirst && outcome.Result.SavedCommitHash != null) _commitMessage.value = string.Empty;
                SetError(null);
                SetNotice(RestoreFlow.Describe(target, outcome.Result));
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

        // ---- stash tab ----

        private void RenderStashes()
        {
            _stashList.Clear();
            _stashEmpty.EnableInClassList(HiddenClass, _stashes.Count > 0);
            foreach (var stash in _stashes)
            {
                var row = new VisualElement { name = "stash-item" };
                row.AddToClassList("shiori-aside-item");
                var selector = new Label(stash.Selector);
                selector.AddToClassList("shiori-file-status");
                selector.AddToClassList("shiori-detail-hash");
                var message = new Label(stash.Message);
                message.AddToClassList("shiori-aside-label");
                message.tooltip = stash.Message;
                var time = new Label(stash.Time == DateTimeOffset.MinValue ? string.Empty : RelativeTime.Format(stash.Time, DateTimeOffset.Now));
                time.AddToClassList("shiori-history-time");
                var apply = new Button(() => ApplyStash(stash)) { text = L10n.Tr("detail.stash.apply") };
                apply.AddToClassList("shiori-small-button");
                row.Add(selector);
                row.Add(message);
                row.Add(time);
                row.Add(apply);
                _stashList.Add(row);
            }
        }

        /// <summary>git stash apply, under the same no-conflict rule as 取り出す; the stash is kept.</summary>
        private async void ApplyStash(StashEntry stash)
        {
            if (_busy) return;
            if (!UnitySaver.SaveForRestoreOrCancel()) return;
            _busy = true;
            SetNotice(null);
            try
            {
                if (!EditorUtility.DisplayDialog(
                        L10n.Tr("detail.stash.dialog.title"),
                        L10n.Tr("detail.stash.dialog.body", stash.Selector, stash.Message),
                        L10n.Tr("detail.stash.apply"),
                        L10n.Tr("restore.dialog.cancel"))) return;

                SetAsidePlan plan;
                using (GitActivity.Begin(L10n.Tr("detail.stash.progress")))
                {
                    plan = await SetAsideOperation.RunAsync(_repo, stash, CancellationToken.None);
                }
                SetError(null);
                SetNotice(DescribeStashApply(plan));
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

        internal static string DescribeStashApply(SetAsidePlan plan)
        {
            switch (plan.Block)
            {
                case SetAsideBlock.WorkingTreeHasChanges: return L10n.Tr("detail.stash.dirty");
                case SetAsideBlock.Overlap: return L10n.Tr("detail.stash.overlap", SimpleModeView.ListPaths(plan.OverlappingPaths, 5));
                default: return L10n.Tr("detail.stash.done", plan.Paths.Count);
            }
        }

        // ---- messages ----

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
            if (ex is RemoteOperationException remote) return RemoteText.Describe(remote);
            if (ex is GitException git) return L10n.Tr("error.git", git.ExitCode, git.Stderr.Trim());
            return L10n.Tr("error.generic", ex.Message);
        }
    }
}
