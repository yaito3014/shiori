using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Counts of changed files, grouped the way the simple-mode UI reports them.</summary>
    public sealed class ChangeStats
    {
        public int Added { get; }
        public int Modified { get; }
        public int Deleted { get; }

        public int Total => Added + Modified + Deleted;

        public ChangeStats(int added, int modified, int deleted)
        {
            Added = added;
            Modified = modified;
            Deleted = deleted;
        }

        public static ChangeStats From(IEnumerable<FileChange> changes)
        {
            int added = 0, modified = 0, deleted = 0;
            foreach (var change in changes)
            {
                switch (change.Kind)
                {
                    case ChangeKind.Added:
                    case ChangeKind.Untracked:
                    case ChangeKind.Copied:
                        added++;
                        break;
                    case ChangeKind.Deleted:
                        deleted++;
                        break;
                    case ChangeKind.Ignored:
                        break;
                    default:
                        modified++;
                        break;
                }
            }
            return new ChangeStats(added, modified, deleted);
        }

        public override string ToString()
        {
            return $"{Total} files changed ({Added} added, {Modified} modified, {Deleted} deleted)";
        }
    }
}
