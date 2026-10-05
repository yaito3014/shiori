using System;
using System.Collections.Generic;
using System.IO;

namespace Shiori
{
    public sealed class MetaCheckResult
    {
        /// <summary>Files and folders under Assets that have no companion .meta (project-relative, forward slashes).</summary>
        public IReadOnlyList<string> MissingMeta { get; }

        /// <summary>.meta files whose asset no longer exists (project-relative, forward slashes).</summary>
        public IReadOnlyList<string> OrphanMeta { get; }

        public bool HasIssues => MissingMeta.Count > 0 || OrphanMeta.Count > 0;

        public MetaCheckResult(IReadOnlyList<string> missingMeta, IReadOnlyList<string> orphanMeta)
        {
            MissingMeta = missingMeta ?? Array.Empty<string>();
            OrphanMeta = orphanMeta ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Walks <c>Assets/</c> on the file system and reports assets without .meta files and .meta files
    /// without assets, following Unity's own rules for which entries are ignored.
    /// </summary>
    internal static class MetaChecker
    {
        public static MetaCheckResult Check(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new ArgumentException("project root is required", nameof(projectRoot));
            var assets = Path.Combine(projectRoot, "Assets");
            var missing = new List<string>();
            var orphan = new List<string>();
            if (Directory.Exists(assets)) Walk(new DirectoryInfo(assets), projectRoot, missing, orphan);
            missing.Sort(StringComparer.Ordinal);
            orphan.Sort(StringComparer.Ordinal);
            return new MetaCheckResult(missing, orphan);
        }

        private static void Walk(DirectoryInfo directory, string projectRoot, List<string> missing, List<string> orphan)
        {
            var entries = directory.GetFileSystemInfos();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries) names.Add(entry.Name);

            foreach (var entry in entries)
            {
                var isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
                if (IsIgnoredByUnity(entry.Name, isDirectory)) continue;

                if (!isDirectory && entry.Name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    var assetName = entry.Name.Substring(0, entry.Name.Length - ".meta".Length);
                    if (assetName.Length == 0 || !names.Contains(assetName) || IsIgnoredByUnity(assetName, Directory.Exists(Path.Combine(directory.FullName, assetName))))
                    {
                        orphan.Add(Relative(projectRoot, entry.FullName));
                    }
                    continue;
                }

                if (!names.Contains(entry.Name + ".meta"))
                {
                    missing.Add(Relative(projectRoot, entry.FullName));
                }

                if (isDirectory) Walk((DirectoryInfo)entry, projectRoot, missing, orphan);
            }
        }

        /// <summary>
        /// Unity skips: names starting with '.', names ending with '~', the folder "cvs",
        /// and files with the ".tmp" extension. Such entries never get .meta files.
        /// </summary>
        internal static bool IsIgnoredByUnity(string name, bool isDirectory)
        {
            if (name.Length == 0) return true;
            if (name[0] == '.') return true;
            if (name[name.Length - 1] == '~') return true;
            if (isDirectory && string.Equals(name, "cvs", StringComparison.OrdinalIgnoreCase)) return true;
            if (!isDirectory && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Relative(string root, string fullPath)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var full = Path.GetFullPath(fullPath);
            var relative = full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
            return relative.Replace('\\', '/');
        }
    }
}
