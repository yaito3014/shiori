using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>One line in a file list: an asset and, when present, its .meta folded into the same row.</summary>
    public sealed class ChangeRow
    {
        /// <summary>Repository-relative path of the asset (never ends with .meta).</summary>
        public string Path { get; }

        /// <summary>Path shown to the user: relative to Assets/ when inside it, otherwise the full path.</summary>
        public string DisplayPath { get; }

        public ChangeKind Kind { get; }

        /// <summary>True when the .meta of this asset changed too.</summary>
        public bool HasMeta { get; }

        /// <summary>True when only the .meta changed (for example a folder's meta) and the asset itself is not listed.</summary>
        public bool MetaOnly { get; }

        public ChangeRow(string path, string displayPath, ChangeKind kind, bool hasMeta, bool metaOnly)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            DisplayPath = displayPath ?? path;
            Kind = kind;
            HasMeta = hasMeta;
            MetaOnly = metaOnly;
        }
    }

    public static class ChangeRowBuilder
    {
        private const string AssetsPrefix = "Assets/";
        private const string MetaSuffix = ".meta";

        /// <summary>Folds each .meta into its asset's row, keeping the original order of first appearance.</summary>
        public static IReadOnlyList<ChangeRow> Build(IReadOnlyList<FileChange> changes)
        {
            if (changes == null) return Array.Empty<ChangeRow>();

            var order = new List<string>();
            var assets = new Dictionary<string, FileChange>(StringComparer.Ordinal);
            var metas = new Dictionary<string, FileChange>(StringComparer.Ordinal);

            foreach (var change in changes)
            {
                var isMeta = change.Path.EndsWith(MetaSuffix, StringComparison.OrdinalIgnoreCase);
                var key = isMeta ? change.Path.Substring(0, change.Path.Length - MetaSuffix.Length) : change.Path;
                if (key.Length == 0) key = change.Path;

                if (isMeta)
                {
                    if (!metas.ContainsKey(key)) metas[key] = change;
                }
                else
                {
                    if (!assets.ContainsKey(key)) assets[key] = change;
                }
                if (!assets.ContainsKey(key) && !metas.ContainsKey(key)) continue;
                if (!order.Contains(key)) order.Add(key);
            }

            var rows = new List<ChangeRow>(order.Count);
            foreach (var key in order)
            {
                var hasAsset = assets.TryGetValue(key, out var asset);
                var hasMeta = metas.TryGetValue(key, out var meta);
                var kind = hasAsset ? asset.Kind : meta.Kind;
                rows.Add(new ChangeRow(key, ToDisplayPath(key), kind, hasMeta, !hasAsset));
            }
            return rows;
        }

        internal static string ToDisplayPath(string path)
        {
            return path.StartsWith(AssetsPrefix, StringComparison.Ordinal) && path.Length > AssetsPrefix.Length
                ? path.Substring(AssetsPrefix.Length)
                : path;
        }
    }
}
