using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Shiori.Editor
{
    /// <summary>Resolves asset GUIDs with the AssetDatabase; results are cached for the life of one diff.</summary>
    internal sealed class AssetGuidResolver : IGuidResolver
    {
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Resolve(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            if (_cache.TryGetValue(guid, out var path)) return path;
            path = AssetDatabase.GUIDToAssetPath(guid);
            path = string.IsNullOrEmpty(path) ? null : path;
            _cache[guid] = path;
            return path;
        }
    }

    internal enum UnityDiffRowKind
    {
        Object,
        Property,
        Message,
    }

    /// <summary>One line of the Unity view: an object heading or one of its properties.</summary>
    internal sealed class UnityDiffRow
    {
        public UnityDiffRowKind Kind { get; }
        public UnityObjectChangeKind Change { get; }
        public string Text { get; }

        public UnityDiffRow(UnityDiffRowKind kind, UnityObjectChangeKind change, string text)
        {
            Kind = kind;
            Change = change;
            Text = text ?? string.Empty;
        }
    }

    /// <summary>
    /// The Unity view of a diff in detail mode (A3, read-only): which GameObjects, components and
    /// material properties changed, instead of raw YAML lines. Only for file types whose YAML the
    /// parser is known to read well; everything else keeps the text diff.
    /// </summary>
    internal static class UnityDiffPresenter
    {
        private static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".prefab", ".mat" };

        public static bool IsSupported(string path)
        {
            return !string.IsNullOrEmpty(path) && Supported.Contains(Path.GetExtension(path));
        }

        /// <summary>
        /// The rows to show for one side of a change: what is staged (HEAD against the index) or what is
        /// not (the index against the file on disk).
        /// </summary>
        public static async Task<List<UnityDiffRow>> LoadAsync(IGitRepository repository, string projectRoot, FileChange change, bool staged, CancellationToken cancellationToken)
        {
            string before, after;
            if (staged)
            {
                var head = await repository.GetHeadAsync(cancellationToken);
                before = head == null ? null : await repository.ReadFileAtAsync("HEAD", change.OldPath ?? change.Path, cancellationToken);
                after = await repository.ReadFileAtAsync(":0", change.Path, cancellationToken);
            }
            else
            {
                before = change.Kind == ChangeKind.Untracked ? null : await repository.ReadFileAtAsync(":0", change.Path, cancellationToken);
                var file = Path.Combine(projectRoot, change.Path.Replace('/', Path.DirectorySeparatorChar));
                after = File.Exists(file) ? File.ReadAllText(file, new UTF8Encoding(false)) : null;
            }
            // Resolve GUIDs on the main thread (AssetDatabase), parse and compare on a worker: big prefabs take a moment.
            var resolver = new PrefetchedResolver(new AssetGuidResolver(), before, after);
            var changes = await Task.Run(() => UnityYamlDiff.Compare(before, after, resolver), cancellationToken);
            return BuildRows(changes);
        }

        internal static List<UnityDiffRow> BuildRows(IReadOnlyList<UnityObjectChange> changes)
        {
            var rows = new List<UnityDiffRow>();
            if (changes.Count == 0)
            {
                rows.Add(new UnityDiffRow(UnityDiffRowKind.Message, UnityObjectChangeKind.Changed, L10n.Tr("detail.unity.none")));
                return rows;
            }
            foreach (var change in changes)
            {
                var kind = L10n.Tr("detail.unity." + change.Kind.ToString().ToLowerInvariant());
                var where = change.Location.Length == 0 ? string.Empty : " (" + change.Location + ")";
                rows.Add(new UnityDiffRow(UnityDiffRowKind.Object, change.Kind, kind + "  " + change.TypeName + where));
                foreach (var property in change.Properties)
                {
                    string text;
                    if (property.OldValue == null) text = property.Path + ": " + property.NewValue;
                    else if (property.NewValue == null) text = property.Path + ": " + property.OldValue;
                    else text = property.Path + ": " + property.OldValue + "  →  " + property.NewValue;
                    var propertyKind = property.OldValue == null ? UnityObjectChangeKind.Added
                        : property.NewValue == null ? UnityObjectChangeKind.Removed
                        : UnityObjectChangeKind.Changed;
                    rows.Add(new UnityDiffRow(UnityDiffRowKind.Property, propertyKind, text));
                }
            }
            return rows;
        }

        /// <summary>
        /// The AssetDatabase may only be called on the main thread, so every GUID in both versions is
        /// resolved up front and the worker thread reads from this table.
        /// </summary>
        private sealed class PrefetchedResolver : IGuidResolver
        {
            private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public PrefetchedResolver(IGuidResolver inner, params string[] texts)
            {
                foreach (var text in texts)
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    var index = 0;
                    while ((index = text.IndexOf("guid: ", index, StringComparison.Ordinal)) >= 0)
                    {
                        index += 6;
                        var end = index;
                        while (end < text.Length && Uri.IsHexDigit(text[end])) end++;
                        if (end - index == 32)
                        {
                            var guid = text.Substring(index, 32);
                            if (!_paths.ContainsKey(guid)) _paths[guid] = inner.Resolve(guid);
                        }
                        index = end;
                    }
                }
            }

            public string Resolve(string guid)
            {
                return guid != null && _paths.TryGetValue(guid, out var path) ? path : null;
            }
        }
    }
}
