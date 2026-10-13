using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Shiori.Editor
{
    /// <summary>
    /// The merge-in-progress panel shown at the top of both modes: what is going on, one row per conflicted file (an
    /// asset and its .meta share a row and are decided together) with 自分 / 相手 buttons, and a button to give up.
    /// In detail mode the wording is git's and a finish button appears once nothing is left. The panel only raises
    /// events; the view runs them so its busy state and messages stay in one place.
    /// </summary>
    internal sealed class ConflictPanel : VisualElement
    {
        private const string HiddenClass = "shiori-hidden";

        private readonly bool _gitTerms;
        private readonly Label _title;
        private readonly Label _help;
        private readonly VisualElement _rows;
        private readonly VisualElement _bulk;
        private readonly Button _finish;

        /// <summary>The user chose a side for some files.</summary>
        public event Action<IReadOnlyList<ConflictEntry>, ConflictSide> ResolveRequested;

        public event Action AbortRequested;

        /// <summary>Detail mode: record the merge (only offered once nothing is undecided).</summary>
        public event Action FinishRequested;

        public ConflictPanel(bool gitTerms)
        {
            _gitTerms = gitTerms;
            name = "merge-panel";
            AddToClassList("shiori-panel");
            AddToClassList("shiori-merge-panel");

            _title = new Label();
            _title.AddToClassList("shiori-panel-title");
            Add(_title);
            _help = new Label();
            _help.AddToClassList("shiori-muted");
            _help.AddToClassList("shiori-merge-help");
            Add(_help);
            _rows = new VisualElement();
            _rows.AddToClassList("shiori-merge-list");
            Add(_rows);

            var actions = new VisualElement();
            actions.AddToClassList("shiori-row");
            actions.AddToClassList("shiori-merge-actions");
            _bulk = new VisualElement();
            _bulk.AddToClassList("shiori-row");
            _bulk.Add(SmallButton(Key("all.ours"), () => ResolveRequested?.Invoke(new List<ConflictEntry>(_pending), ConflictSide.Ours)));
            _bulk.Add(SmallButton(Key("all.theirs"), () => ResolveRequested?.Invoke(new List<ConflictEntry>(_pending), ConflictSide.Theirs)));
            actions.Add(_bulk);
            _finish = SmallButton(Key("finish"), () => FinishRequested?.Invoke());
            _finish.AddToClassList("shiori-merge-finish");
            actions.Add(_finish);
            var abort = SmallButton(Key("abort"), () => AbortRequested?.Invoke());
            abort.tooltip = L10n.Tr(Key("abort.tooltip"));
            actions.Add(abort);
            Add(actions);

            Render(MergeState.None);
        }

        private readonly List<ConflictEntry> _pending = new List<ConflictEntry>();

        private string Key(string key) => (_gitTerms ? "merge.detail." : "merge.") + key;

        private static Button SmallButton(string key, Action clicked)
        {
            var button = new Button(clicked) { text = L10n.Tr(key) };
            button.AddToClassList("shiori-small-button");
            return button;
        }

        /// <summary>Hidden unless a merge is in progress.</summary>
        public void Render(MergeState state)
        {
            state = state ?? MergeState.None;
            EnableInClassList(HiddenClass, !state.InProgress);
            _pending.Clear();
            _pending.AddRange(state.Conflicts);
            _rows.Clear();
            if (!state.InProgress) return;

            var waiting = state.Conflicts.Count > 0;
            var rows = Rows(state.Conflicts);
            _title.text = L10n.Tr(Key("title"));
            _help.text = waiting ? L10n.Tr(Key("help"), rows.Count) : L10n.Tr(Key("ready"));
            foreach (var row in rows) _rows.Add(MakeRow(row));
            _bulk.EnableInClassList(HiddenClass, rows.Count < 2);
            _finish.EnableInClassList(HiddenClass, !_gitTerms || waiting);
        }

        /// <summary>One row per asset: its .meta is decided with it, as everywhere else in Shiori.</summary>
        internal static IReadOnlyList<ConflictRow> Rows(IReadOnlyList<ConflictEntry> conflicts)
        {
            var byPath = new Dictionary<string, ConflictEntry>(StringComparer.Ordinal);
            var changes = new List<FileChange>(conflicts.Count);
            foreach (var conflict in conflicts)
            {
                byPath[conflict.Path] = conflict;
                changes.Add(new FileChange(conflict.Path, ChangeKind.Unmerged, 'U', 'U'));
            }
            var rows = new List<ConflictRow>();
            foreach (var row in ChangeRowBuilder.Build(changes))
            {
                var entries = new List<ConflictEntry>();
                foreach (var file in row.Files) entries.Add(byPath[file.Path]);
                rows.Add(new ConflictRow(row, entries));
            }
            return rows;
        }

        private VisualElement MakeRow(ConflictRow row)
        {
            var element = new VisualElement();
            element.AddToClassList("shiori-merge-item");
            var path = new Label(row.Row.MetaOnly ? L10n.Tr("row.metaonly", row.Row.DisplayPath) : row.Row.DisplayPath) { tooltip = row.Row.Path };
            path.AddToClassList("shiori-merge-path");
            path.RegisterCallback<ClickEvent>(_ => AssetNavigator.Reveal(row.Row.Path));
            element.Add(path);
            if (row.Row.HasMeta && !row.Row.MetaOnly)
            {
                var meta = new Label(".meta");
                meta.AddToClassList("shiori-file-meta");
                element.Add(meta);
            }
            var note = Note(row.Entries);
            if (note != null)
            {
                var label = new Label(note);
                label.AddToClassList("shiori-muted");
                label.AddToClassList("shiori-merge-note");
                element.Add(label);
            }
            element.Add(SmallButton(Key("ours"), () => ResolveRequested?.Invoke(row.Entries, ConflictSide.Ours)));
            element.Add(SmallButton(Key("theirs"), () => ResolveRequested?.Invoke(row.Entries, ConflictSide.Theirs)));
            return element;
        }

        /// <summary>Says when choosing a side deletes the file; null when both sides have it.</summary>
        private string Note(IReadOnlyList<ConflictEntry> entries)
        {
            var oursDeleted = false;
            var theirsDeleted = false;
            foreach (var entry in entries)
            {
                oursDeleted |= entry.IsDeletedOn(ConflictSide.Ours);
                theirsDeleted |= entry.IsDeletedOn(ConflictSide.Theirs);
            }
            if (oursDeleted) return L10n.Tr(Key("deleted.ours"));
            if (theirsDeleted) return L10n.Tr(Key("deleted.theirs"));
            return null;
        }

        /// <summary>A displayed row and the conflict entries it stands for.</summary>
        internal sealed class ConflictRow
        {
            public ChangeRow Row { get; }
            public IReadOnlyList<ConflictEntry> Entries { get; }

            public ConflictRow(ChangeRow row, IReadOnlyList<ConflictEntry> entries)
            {
                Row = row;
                Entries = entries;
            }
        }
    }
}
