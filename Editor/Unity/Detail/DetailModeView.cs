using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>Detail mode, M1 subset (F5): the working-tree change list and a read-only unified diff.</summary>
    internal sealed class DetailModeView : VisualElement, IShioriView
    {
        public const int MaxDiffLines = 4000;
        private const string HiddenClass = "shiori-hidden";

        /// <summary>The editor's own monospace font (used by the Console). Loaded once per domain.</summary>
        internal static readonly string[] MonospaceFontPaths =
        {
            "Fonts/RobotoMono/RobotoMono-Regular.ttf",
            "Fonts/robotomono/RobotoMono-Regular.ttf",
        };

        private static Font _monospace;
        private static bool _monospaceSearched;

        private readonly IGitRepository _repo;
        private readonly Label _error;
        private readonly Label _filesEmpty;
        private readonly ListView _filesList;
        private readonly Label _diffTitle;
        private readonly Label _diffEmpty;
        private readonly Label _diffTruncated;
        private readonly ListView _diffLines;

        private readonly List<FileChange> _changes = new List<FileChange>();
        private readonly List<DiffLine> _lines = new List<DiffLine>();
        private bool _busy;
        private string _pendingSelection;
        private int _diffGeneration;

        public string SelectedPath { get; private set; }

        public event Action<string> SelectionChanged;

        public DetailModeView(ShioriSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            _repo = session.Repository ?? throw new InvalidOperationException("git repository is not available");

            UiAssets.Tree("DetailModeView.uxml").CloneTree(this);
            // The cloned tree sits inside this element; without this the split view collapses to zero height.
            style.flexGrow = 1;

            _error = this.Q<Label>("view-error");
            this.Q<Label>("files-title").text = L10n.Tr("detail.files.title");
            var refresh = this.Q<Button>("files-refresh");
            refresh.text = L10n.Tr("detail.refresh");
            refresh.clicked += RefreshStatus;
            _filesEmpty = this.Q<Label>("files-empty");
            _filesEmpty.text = L10n.Tr("detail.files.empty");
            _filesList = this.Q<ListView>("files-list");
            _filesList.makeItem = MakeFileItem;
            _filesList.bindItem = BindFileItem;
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

            SetError(null);
            ShowDiff(null, null, false);
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

        public void RefreshAll()
        {
            RefreshStatus();
        }

        public async void RefreshStatus()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var status = await _repo.GetStatusAsync(CancellationToken.None);
                _changes.Clear();
                _changes.AddRange(status.Changes);
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
        }

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

        private void BindFileItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _changes.Count) return;
            var change = _changes[index];
            element.Q<Label>("status").text = new string(new[] { change.IndexStatus, change.WorktreeStatus });
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
