using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    internal sealed class GitRepository : IGitRepository
    {
        private readonly IGitRunner _runner;
        private readonly string _git;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public string WorkingDirectory { get; }

        public GitRepository(IGitRunner runner, string gitExecutable, string workingDirectory)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            if (string.IsNullOrEmpty(gitExecutable)) throw new ArgumentException("git executable is required", nameof(gitExecutable));
            if (string.IsNullOrEmpty(workingDirectory)) throw new ArgumentException("working directory is required", nameof(workingDirectory));
            _git = gitExecutable;
            WorkingDirectory = workingDirectory;
        }

        public async Task<RepositoryProbe> ProbeAsync(CancellationToken cancellationToken)
        {
            var result = await RunAllowingFailureAsync(cancellationToken, "rev-parse", "--show-toplevel").ConfigureAwait(false);
            if (!result.Succeeded) return new RepositoryProbe(RepositoryState.NotARepository, null);

            var topLevel = result.Stdout.Trim();
            var state = PathsEqual(topLevel, WorkingDirectory) ? RepositoryState.Ready : RepositoryState.RootMismatch;
            return new RepositoryProbe(state, topLevel);
        }

        public async Task InitAsync(CancellationToken cancellationToken)
        {
            await RunAsync(cancellationToken, "init", "-q").ConfigureAwait(false);
        }

        public async Task<WorktreeStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            var result = await RunAsync(cancellationToken, "status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all").ConfigureAwait(false);
            return StatusParser.Parse(result.Stdout);
        }

        public async Task AddAllAsync(CancellationToken cancellationToken)
        {
            await RunAsync(cancellationToken, "add", "-A").ConfigureAwait(false);
        }

        public async Task<string> CommitAsync(string message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("commit message is required", nameof(message));
            await RunAsync(cancellationToken, "commit", "-q", "-m", message).ConfigureAwait(false);
            var head = await GetHeadAsync(cancellationToken).ConfigureAwait(false);
            return head ?? throw new GitException("commit", 0, "HEAD is missing after commit");
        }

        public async Task<string> GetHeadAsync(CancellationToken cancellationToken)
        {
            var result = await RunAllowingFailureAsync(cancellationToken, "rev-parse", "-q", "--verify", "HEAD").ConfigureAwait(false);
            if (!result.Succeeded) return null;
            var hash = result.Stdout.Trim();
            return hash.Length == 0 ? null : hash;
        }

        public async Task<IReadOnlyList<Snapshot>> GetLogAsync(int maxCount, int skip, CancellationToken cancellationToken)
        {
            if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));

            var head = await GetHeadAsync(cancellationToken).ConfigureAwait(false);
            if (head == null) return Array.Empty<Snapshot>();

            var result = await RunAsync(cancellationToken,
                "log", "-z", "--format=" + LogParser.Format, "--name-status",
                "--max-count=" + maxCount, "--skip=" + skip).ConfigureAwait(false);
            return LogParser.Parse(result.Stdout);
        }

        public async Task<IReadOnlyList<FileChange>> GetChangedFilesAsync(string hash, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(hash)) throw new ArgumentException("hash is required", nameof(hash));
            var result = await RunAsync(cancellationToken, "show", "--name-status", "--format=", "-z", hash, "--").ConfigureAwait(false);
            return NameStatusParser.Parse(result.Stdout);
        }

        public async Task<string> GetDiffAsync(string path, bool untracked, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is required", nameof(path));

            GitResult result;
            if (untracked)
            {
                // --no-index exits 1 when the files differ, which is the expected case here.
                result = await RunAllowingFailureAsync(cancellationToken, "diff", "--no-color", "--no-ext-diff", "--no-index", "--", "/dev/null", path).ConfigureAwait(false);
                if (result.ExitCode != 0 && result.ExitCode != 1) throw new GitException("diff --no-index", result.ExitCode, result.Stderr);
            }
            else
            {
                var head = await GetHeadAsync(cancellationToken).ConfigureAwait(false);
                result = head == null
                    ? await RunAsync(cancellationToken, "diff", "--no-color", "--no-ext-diff", "--cached", "--", path).ConfigureAwait(false)
                    : await RunAsync(cancellationToken, "diff", "--no-color", "--no-ext-diff", "HEAD", "--", path).ConfigureAwait(false);
            }
            return result.Stdout;
        }

        public async Task ReadTreeAsync(string hash, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(hash)) throw new ArgumentException("hash is required", nameof(hash));
            await RunAsync(cancellationToken, "read-tree", "-u", "--reset", hash).ConfigureAwait(false);
        }

        public async Task<string> StashPushAsync(string message, bool includeUntracked, CancellationToken cancellationToken)
        {
            var before = await GetStashHeadAsync(cancellationToken).ConfigureAwait(false);

            var args = new List<string> { "stash", "push", "-q" };
            if (includeUntracked) args.Add("-u");
            if (!string.IsNullOrEmpty(message))
            {
                args.Add("-m");
                args.Add(message);
            }
            await RunAsync(cancellationToken, args.ToArray()).ConfigureAwait(false);

            var after = await GetStashHeadAsync(cancellationToken).ConfigureAwait(false);
            return after != null && after != before ? after : null;
        }

        public async Task<IReadOnlyList<StashEntry>> StashListAsync(CancellationToken cancellationToken)
        {
            var result = await RunAsync(cancellationToken, "stash", "list", "-z", "--format=" + StashListParser.Format).ConfigureAwait(false);
            return StashListParser.Parse(result.Stdout);
        }

        public async Task StashApplyAsync(string stashHash, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stashHash)) throw new ArgumentException("stash hash is required", nameof(stashHash));
            await RunAsync(cancellationToken, "stash", "apply", "-q", stashHash).ConfigureAwait(false);
        }

        public async Task<bool> RevisionExistsAsync(string revision, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(revision)) throw new ArgumentException("revision is required", nameof(revision));
            var result = await RunAllowingFailureAsync(cancellationToken, "rev-parse", "-q", "--verify", revision + "^{commit}").ConfigureAwait(false);
            return result.Succeeded && result.Stdout.Trim().Length > 0;
        }

        public async Task<IReadOnlyList<string>> GetChangedPathsAsync(string from, string to, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(from)) throw new ArgumentException("from is required", nameof(from));
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("to is required", nameof(to));
            var result = await RunAsync(cancellationToken, "diff", "--name-only", "--no-renames", "-z", from, to, "--").ConfigureAwait(false);
            return Tokenizer.SplitNul(result.Stdout);
        }

        public async Task<IReadOnlyList<string>> GetTreePathsAsync(string revision, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(revision)) throw new ArgumentException("revision is required", nameof(revision));
            var result = await RunAsync(cancellationToken, "ls-tree", "-r", "-z", "--name-only", revision).ConfigureAwait(false);
            return Tokenizer.SplitNul(result.Stdout);
        }

        public async Task<GitIdentity> GetIdentityAsync(CancellationToken cancellationToken)
        {
            var name = await GetConfigAsync("user.name", cancellationToken).ConfigureAwait(false);
            var email = await GetConfigAsync("user.email", cancellationToken).ConfigureAwait(false);
            return new GitIdentity(name, email);
        }

        public async Task SetIdentityAsync(string name, string email, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("email is required", nameof(email));
            await SetLocalConfigAsync("user.name", name.Trim(), cancellationToken).ConfigureAwait(false);
            await SetLocalConfigAsync("user.email", email.Trim(), cancellationToken).ConfigureAwait(false);
        }

        internal async Task SetLocalConfigAsync(string key, string value, CancellationToken cancellationToken)
        {
            await RunAsync(cancellationToken, "config", "--local", key, value).ConfigureAwait(false);
        }

        private async Task<string> GetConfigAsync(string key, CancellationToken cancellationToken)
        {
            var result = await RunAllowingFailureAsync(cancellationToken, "config", "--get", key).ConfigureAwait(false);
            // Exit code 1 means the key is unset; anything else is a real failure.
            if (result.ExitCode == 1) return null;
            if (!result.Succeeded) throw new GitException("config --get " + key, result.ExitCode, result.Stderr);
            var value = result.Stdout.TrimEnd('\r', '\n');
            return value.Length == 0 ? null : value;
        }

        private async Task<string> GetStashHeadAsync(CancellationToken cancellationToken)
        {
            var result = await RunAllowingFailureAsync(cancellationToken, "rev-parse", "-q", "--verify", "refs/stash").ConfigureAwait(false);
            if (!result.Succeeded) return null;
            var hash = result.Stdout.Trim();
            return hash.Length == 0 ? null : hash;
        }

        private async Task<GitResult> RunAsync(CancellationToken cancellationToken, params string[] args)
        {
            var result = await RunAllowingFailureAsync(cancellationToken, args).ConfigureAwait(false);
            if (!result.Succeeded) throw new GitException(CommandLine.Join(args), result.ExitCode, result.Stderr);
            return result;
        }

        private async Task<GitResult> RunAllowingFailureAsync(CancellationToken cancellationToken, params string[] args)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await _runner.RunAsync(_git, args, WorkingDirectory, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        internal static bool PathsEqual(string a, string b)
        {
            var na = Normalize(a);
            var nb = Normalize(b);
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(na, nb, comparison);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                full = path;
            }
            catch (NotSupportedException)
            {
                full = path;
            }
            return full.Replace('\\', '/').TrimEnd('/');
        }
    }
}
