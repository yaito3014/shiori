using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Parsed <c>git status --porcelain=v2</c>.</summary>
    public sealed class WorktreeStatus
    {
        /// <summary>Current branch name, or null when HEAD is detached.</summary>
        public string Branch { get; }

        /// <summary>HEAD commit hash, or null when the repository has no commits yet.</summary>
        public string HeadHash { get; }

        public bool IsDetached { get; }

        /// <summary>Every tracked change and untracked file. Ignored files are not listed.</summary>
        public IReadOnlyList<FileChange> Changes { get; }

        public ChangeStats Stats { get; }

        public bool HasChanges => Changes.Count > 0;

        public WorktreeStatus(string branch, string headHash, bool isDetached, IReadOnlyList<FileChange> changes)
        {
            Branch = branch;
            HeadHash = headHash;
            IsDetached = isDetached;
            Changes = changes ?? Array.Empty<FileChange>();
            Stats = ChangeStats.From(Changes);
        }
    }
}
