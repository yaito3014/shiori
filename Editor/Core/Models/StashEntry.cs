using System;

namespace Shiori
{
    public sealed class StashEntry
    {
        /// <summary>Reflog selector such as <c>stash@{0}</c>.</summary>
        public string Selector { get; }

        public string Hash { get; }

        /// <summary>The full stash subject, e.g. "On main: shiori:auto-before-restore: first".</summary>
        public string Message { get; }

        /// <summary>When the stash was made, or <see cref="DateTimeOffset.MinValue"/> when unknown.</summary>
        public DateTimeOffset Time { get; }

        public StashEntry(string selector, string hash, string message)
            : this(selector, hash, message, DateTimeOffset.MinValue)
        {
        }

        public StashEntry(string selector, string hash, string message, DateTimeOffset time)
        {
            Selector = selector ?? throw new ArgumentNullException(nameof(selector));
            Hash = hash ?? string.Empty;
            Message = message ?? string.Empty;
            Time = time;
        }
    }
}
