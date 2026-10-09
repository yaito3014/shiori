using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>
    /// Git operations on one working directory. Every method runs git asynchronously and
    /// throws <see cref="GitException"/> when git fails. Calls on one instance are serialised.
    /// None of these methods show confirmation dialogs; the destructive ones
    /// (<see cref="ReadTreeAsync"/>) must only be reached through a confirmed UI action.
    /// </summary>
    public interface IGitRepository
    {
        string WorkingDirectory { get; }

        /// <summary>Checks whether the working directory is a repository root.</summary>
        Task<RepositoryProbe> ProbeAsync(CancellationToken cancellationToken);

        /// <summary><c>git init</c>. Safe to call on an existing repository.</summary>
        Task InitAsync(CancellationToken cancellationToken);

        /// <summary><c>git status --porcelain=v2 -z --branch --untracked-files=all</c>.</summary>
        Task<WorktreeStatus> GetStatusAsync(CancellationToken cancellationToken);

        /// <summary><c>git add -A</c>.</summary>
        Task AddAllAsync(CancellationToken cancellationToken);

        /// <summary><c>git commit -m</c>. Returns the new commit hash.</summary>
        Task<string> CommitAsync(string message, CancellationToken cancellationToken);

        /// <summary>HEAD hash, or null when the repository has no commits.</summary>
        Task<string> GetHeadAsync(CancellationToken cancellationToken);

        /// <summary>Newest-first commits with their changed files. Empty when there are no commits.</summary>
        Task<IReadOnlyList<Snapshot>> GetLogAsync(int maxCount, int skip, CancellationToken cancellationToken);

        /// <summary>Files changed by one commit (<c>git show --name-status</c>).</summary>
        Task<IReadOnlyList<FileChange>> GetChangedFilesAsync(string hash, CancellationToken cancellationToken);

        /// <summary>
        /// Unified diff text of one path between HEAD and the working tree. For an untracked file the
        /// whole file is shown as added. Returns an empty string when there is no difference.
        /// </summary>
        Task<string> GetDiffAsync(string path, bool untracked, CancellationToken cancellationToken);

        /// <summary>
        /// <c>git read-tree -u --reset &lt;hash&gt;</c>: makes the index and tracked files match the commit
        /// without moving HEAD. Overwrites local modifications of tracked files.
        /// </summary>
        Task ReadTreeAsync(string hash, CancellationToken cancellationToken);

        /// <summary><c>git stash push</c>. Returns the stash commit hash, or null when there was nothing to stash.</summary>
        Task<string> StashPushAsync(string message, bool includeUntracked, CancellationToken cancellationToken);

        Task<IReadOnlyList<StashEntry>> StashListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <c>git stash apply</c> of one stash (by hash), keeping the stash. Merges into the working tree,
        /// so callers check for overlapping changes first (see <c>SetAsideRunner</c>) and only apply on a
        /// clean working tree. Throws <see cref="GitException"/> when git refuses or leaves conflicts.
        /// </summary>
        Task StashApplyAsync(string stashHash, CancellationToken cancellationToken);

        /// <summary>True when <paramref name="revision"/> names an existing commit (for example <c>&lt;stash&gt;^3</c>).</summary>
        Task<bool> RevisionExistsAsync(string revision, CancellationToken cancellationToken);

        /// <summary>Paths that differ between two commits (<c>git diff --name-only --no-renames</c>), repository-relative with '/'.</summary>
        Task<IReadOnlyList<string>> GetChangedPathsAsync(string from, string to, CancellationToken cancellationToken);

        /// <summary>
        /// Text of <paramref name="path"/> as stored in <paramref name="revision"/> (UTF-8), or null when the
        /// file does not exist there (or is a folder). <paramref name="path"/> is repository-relative.
        /// </summary>
        Task<string> ReadFileAtAsync(string revision, string path, CancellationToken cancellationToken);

        /// <summary>URL of the 送信先 (the <c>origin</c> remote), or null when none is set.</summary>
        Task<string> GetRemoteUrlAsync(CancellationToken cancellationToken);

        /// <summary>Points <c>origin</c> at <paramref name="url"/>, adding it when absent. Writes only <c>.git/config</c>.</summary>
        Task SetRemoteUrlAsync(string url, CancellationToken cancellationToken);

        /// <summary>
        /// Branch heads at <paramref name="url"/> (<c>git ls-remote --heads</c>) as hash per ref name; empty for an
        /// empty repository. Network operation with a time limit; throws <see cref="RemoteOperationException"/>.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> ListRemoteHeadsAsync(string url, CancellationToken cancellationToken);

        /// <summary>The last known hash of <paramref name="branch"/> on <c>origin</c> (refs/remotes/origin/…), or null.</summary>
        Task<string> GetRemoteTrackingHashAsync(string branch, CancellationToken cancellationToken);

        /// <summary><c>git rev-list --count</c> of a range such as <c>A..HEAD</c> or <c>HEAD</c>.</summary>
        Task<int> CountCommitsAsync(string range, CancellationToken cancellationToken);

        /// <summary>
        /// <c>git push -u origin &lt;branch&gt;</c>, never forced. Network operation with a time limit; throws
        /// <see cref="RemoteOperationException"/> (for example <see cref="RemoteErrorKind.Rejected"/>).
        /// </summary>
        Task PushAsync(string branch, CancellationToken cancellationToken);

        /// <summary>Every file path in a commit's tree (<c>git ls-tree -r --name-only</c>).</summary>
        Task<IReadOnlyList<string>> GetTreePathsAsync(string revision, CancellationToken cancellationToken);

        Task<GitIdentity> GetIdentityAsync(CancellationToken cancellationToken);

        /// <summary>Writes <c>user.name</c> / <c>user.email</c> into the repository-local config.</summary>
        Task SetIdentityAsync(string name, string email, CancellationToken cancellationToken);
    }
}
