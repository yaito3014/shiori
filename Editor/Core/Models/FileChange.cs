using System;

namespace Shiori
{
    /// <summary>One changed path, relative to the repository root, using forward slashes.</summary>
    public sealed class FileChange
    {
        public string Path { get; }

        /// <summary>Previous path for renames and copies; otherwise null.</summary>
        public string OldPath { get; }

        public ChangeKind Kind { get; }

        /// <summary>Raw status letter for the index side ('.' when unchanged, '?' for untracked).</summary>
        public char IndexStatus { get; }

        /// <summary>Raw status letter for the working-tree side ('.' when unchanged, '?' for untracked).</summary>
        public char WorktreeStatus { get; }

        public FileChange(string path, ChangeKind kind, char indexStatus = '.', char worktreeStatus = '.', string oldPath = null)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Kind = kind;
            IndexStatus = indexStatus;
            WorktreeStatus = worktreeStatus;
            OldPath = oldPath;
        }

        public bool IsMeta => Path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);

        /// <summary>The index differs from HEAD: something of this path is staged.</summary>
        public bool IsStaged => IndexStatus != '.' && IndexStatus != '?';

        /// <summary>The working tree differs from the index, including untracked files.</summary>
        public bool IsUnstaged => WorktreeStatus != '.';

        public override string ToString()
        {
            return OldPath == null ? $"{Kind} {Path}" : $"{Kind} {OldPath} -> {Path}";
        }
    }
}
