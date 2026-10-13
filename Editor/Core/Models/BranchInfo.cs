using System;

namespace Shiori
{
    /// <summary>One branch: a local one, or one that so far exists only on <c>origin</c>.</summary>
    public sealed class BranchInfo
    {
        /// <summary>Short name such as <c>main</c> or <c>shiori/衣装A</c>, the same for the local and the origin copy.</summary>
        public string Name { get; }

        /// <summary>Hash of the newest commit on the branch.</summary>
        public string Hash { get; }

        /// <summary>True for the branch HEAD is on.</summary>
        public bool IsCurrent { get; }

        /// <summary>True when there is no local branch yet, only <c>refs/remotes/origin/&lt;Name&gt;</c> (say, made on another PC).</summary>
        public bool IsRemoteOnly { get; }

        /// <summary>True when <c>origin</c> also has this branch (as of the last fetch).</summary>
        public bool IsOnRemote { get; }

        /// <summary>Commit time of <see cref="Hash"/>.</summary>
        public DateTimeOffset Time { get; }

        /// <summary>Subject of <see cref="Hash"/>.</summary>
        public string Subject { get; }

        /// <summary>The ref to read from: <c>refs/heads/…</c>, or <c>refs/remotes/origin/…</c> for a remote-only branch.</summary>
        public string RefName => (IsRemoteOnly ? "refs/remotes/origin/" : "refs/heads/") + Name;

        public BranchInfo(string name, string hash, bool isCurrent, bool isRemoteOnly, bool isOnRemote, DateTimeOffset time, string subject)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            IsCurrent = isCurrent;
            IsRemoteOnly = isRemoteOnly;
            IsOnRemote = isOnRemote || isRemoteOnly;
            Time = time;
            Subject = subject ?? string.Empty;
        }

        /// <summary>The newest commit as a <see cref="Snapshot"/> without file changes, for previews and dialogs.</summary>
        public Snapshot ToSnapshot()
        {
            return new Snapshot(Hash, Time, Subject, string.Empty, null);
        }
    }
}
