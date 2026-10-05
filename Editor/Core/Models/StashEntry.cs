using System;

namespace Shiori
{
    public sealed class StashEntry
    {
        /// <summary>Reflog selector such as <c>stash@{0}</c>.</summary>
        public string Selector { get; }

        public string Hash { get; }

        /// <summary>The full stash subject, e.g. "On main: shiori:auto-before-restore".</summary>
        public string Message { get; }

        public StashEntry(string selector, string hash, string message)
        {
            Selector = selector ?? throw new ArgumentNullException(nameof(selector));
            Hash = hash ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
