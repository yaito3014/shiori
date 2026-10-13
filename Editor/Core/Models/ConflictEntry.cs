using System;

namespace Shiori
{
    /// <summary>Which side of a stopped merge to keep: this PC's (or the current branch's), or the one being merged in.</summary>
    public enum ConflictSide
    {
        Ours,
        Theirs,
    }

    /// <summary>
    /// One path git could not merge, with the blob of each side as the index holds it (<c>git ls-files -u</c>).
    /// A side's hash is null when that side deleted the file.
    /// </summary>
    public sealed class ConflictEntry
    {
        /// <summary>Repository-relative, with '/'.</summary>
        public string Path { get; }

        /// <summary>The common ancestor's blob, or null when both sides added the file.</summary>
        public string BaseHash { get; }

        public string OursHash { get; }

        public string TheirsHash { get; }

        public ConflictEntry(string path, string baseHash, string oursHash, string theirsHash)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is required", nameof(path));
            Path = path;
            BaseHash = baseHash;
            OursHash = oursHash;
            TheirsHash = theirsHash;
        }

        public string HashOf(ConflictSide side) => side == ConflictSide.Ours ? OursHash : TheirsHash;

        /// <summary>True when keeping <paramref name="side"/> means the file is gone.</summary>
        public bool IsDeletedOn(ConflictSide side) => HashOf(side) == null;
    }
}
