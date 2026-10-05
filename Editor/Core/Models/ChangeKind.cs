namespace Shiori
{
    /// <summary>Simplified classification of a file change, shared by working-tree status and commit diffs.</summary>
    public enum ChangeKind
    {
        Added,
        Modified,
        Deleted,
        Renamed,
        Copied,
        TypeChanged,
        Untracked,
        Ignored,
        Unmerged,
        Unknown,
    }
}
