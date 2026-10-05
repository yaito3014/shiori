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

        Task<GitIdentity> GetIdentityAsync(CancellationToken cancellationToken);

        /// <summary>Writes <c>user.name</c> / <c>user.email</c> into the repository-local config.</summary>
        Task SetIdentityAsync(string name, string email, CancellationToken cancellationToken);
    }
}
