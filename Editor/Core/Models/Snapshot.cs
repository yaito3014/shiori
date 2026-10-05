using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>One commit as shown in the history view.</summary>
    public sealed class Snapshot
    {
        public string Hash { get; }
        public DateTimeOffset Time { get; }

        /// <summary>The commit subject (first paragraph, folded onto one line).</summary>
        public string Message { get; }

        public string Author { get; }
        public IReadOnlyList<FileChange> Changes { get; }
        public ChangeStats Stats { get; }

        public string ShortHash => Hash.Length > 7 ? Hash.Substring(0, 7) : Hash;

        public Snapshot(string hash, DateTimeOffset time, string message, string author, IReadOnlyList<FileChange> changes)
        {
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Time = time;
            Message = message ?? string.Empty;
            Author = author ?? string.Empty;
            Changes = changes ?? Array.Empty<FileChange>();
            Stats = ChangeStats.From(Changes);
        }
    }
}
