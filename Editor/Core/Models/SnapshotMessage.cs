using System.Globalization;

namespace Shiori
{
    /// <summary>Builds the commit message used when the user leaves the memo empty (F2).</summary>
    public static class SnapshotMessage
    {
        public static string Build(ChangeStats stats)
        {
            if (stats == null) stats = new ChangeStats(0, 0, 0);
            return string.Format(CultureInfo.InvariantCulture,
                "Snapshot: {0} files changed ({1} added, {2} modified, {3} deleted)",
                stats.Total, stats.Added, stats.Modified, stats.Deleted);
        }

        /// <summary>Uses the user's memo when it has content, otherwise the generated message.</summary>
        public static string Resolve(string memo, ChangeStats stats)
        {
            var trimmed = memo?.Trim();
            return string.IsNullOrEmpty(trimmed) ? Build(stats) : trimmed;
        }
    }
}
