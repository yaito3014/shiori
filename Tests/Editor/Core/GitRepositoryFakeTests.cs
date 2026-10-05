using System;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>Verifies the exact git invocations and error handling without a real git.</summary>
    public class GitRepositoryFakeTests
    {
        private const string Hash = "f01f1197bcb6ce4715db52acbe48a69589e5aeec";
        private FakeGitRunner _runner;
        private GitRepository _repo;

        [SetUp]
        public void SetUp()
        {
            _runner = new FakeGitRunner();
            _repo = new GitRepository(_runner, "git", @"C:\proj");
        }

        [Test]
        public void Status_UsesPorcelainV2WithNulSeparators()
        {
            _repo.GetStatusAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(_runner.Calls[0].Joined, Is.EqualTo("status --porcelain=v2 -z --branch --untracked-files=all"));
            Assert.That(_runner.Calls[0].WorkingDirectory, Is.EqualTo(@"C:\proj"));
            Assert.That(_runner.Calls[0].Executable, Is.EqualTo("git"));
        }

        [Test]
        public void Commit_PassesMessageAsSingleArgumentAndReturnsHead()
        {
            _runner.On("rev-parse -q --verify HEAD", Hash + "\n");
            var hash = _repo.CommitAsync("Snapshot: 3 files changed", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(hash, Is.EqualTo(Hash));
            Assert.That(_runner.Calls[0].Args, Is.EqualTo(new[] { "commit", "-q", "-m", "Snapshot: 3 files changed" }));
        }

        [Test]
        public void Commit_RejectsEmptyMessage()
        {
            Assert.Throws<ArgumentException>(() => _repo.CommitAsync("  ", CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(_runner.Calls, Is.Empty);
        }

        [Test]
        public void Failure_BecomesGitExceptionWithExitCodeAndStderr()
        {
            _runner.On("add -A", "", exitCode: 128, stderr: "fatal: not a git repository");
            var ex = Assert.Throws<GitException>(() => _repo.AddAllAsync(CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(ex.ExitCode, Is.EqualTo(128));
            Assert.That(ex.Stderr, Does.Contain("not a git repository"));
            Assert.That(ex.Command, Is.EqualTo("add -A"));
        }

        [Test]
        public void Log_SkipsGitWhenThereIsNoHead()
        {
            _runner.On("rev-parse -q --verify HEAD", "", exitCode: 1);
            var log = _repo.GetLogAsync(200, 0, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(log, Is.Empty);
            Assert.That(_runner.Calls.Count, Is.EqualTo(1));
        }

        [Test]
        public void Log_RequestsNameStatusWithPaging()
        {
            _runner.On("rev-parse -q --verify HEAD", Hash);
            _repo.GetLogAsync(200, 400, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(_runner.Calls[1].Joined, Is.EqualTo("log -z --format=%x1e%H%x00%at%x00%s%x00%an --name-status --max-count=200 --skip=400"));
        }

        [Test]
        public void ReadTree_UsesResetAndUpdateWorktree()
        {
            _repo.ReadTreeAsync(Hash, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(_runner.Calls[0].Args, Is.EqualTo(new[] { "read-tree", "-u", "--reset", Hash }));
        }

        [Test]
        public void StashPush_ReturnsNullWhenNothingWasStashed()
        {
            _runner.On("rev-parse -q --verify refs/stash", "", exitCode: 1);
            var result = _repo.StashPushAsync("shiori:auto-before-restore", true, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result, Is.Null);
            Assert.That(_runner.Calls[1].Args, Is.EqualTo(new[] { "stash", "push", "-q", "-u", "-m", "shiori:auto-before-restore" }));
        }

        [Test]
        public void Identity_UnsetKeysYieldIncompleteIdentity()
        {
            _runner.On("config --get user.name", "", exitCode: 1);
            _runner.On("config --get user.email", "", exitCode: 1);
            var identity = _repo.GetIdentityAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(identity.IsComplete, Is.False);
        }

        [Test]
        public void SetIdentity_WritesLocalConfig()
        {
            _repo.SetIdentityAsync(" Alice ", "alice@example.com", CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(_runner.Calls[0].Args, Is.EqualTo(new[] { "config", "--local", "user.name", "Alice" }));
            Assert.That(_runner.Calls[1].Args, Is.EqualTo(new[] { "config", "--local", "user.email", "alice@example.com" }));
        }

        [Test]
        public void Probe_NotARepository()
        {
            _runner.On("rev-parse --show-toplevel", "", exitCode: 128, stderr: "fatal: not a git repository");
            var probe = _repo.ProbeAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(probe.State, Is.EqualTo(RepositoryState.NotARepository));
        }

        [Test]
        public void Probe_RootMismatchWhenTopLevelIsAParent()
        {
            _runner.On("rev-parse --show-toplevel", "C:/\n");
            var probe = _repo.ProbeAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(probe.State, Is.EqualTo(RepositoryState.RootMismatch));
            Assert.That(probe.TopLevel, Is.EqualTo("C:/"));
        }

        [Test]
        public void PathsEqual_IgnoresSeparatorStyleAndTrailingSlash()
        {
            Assert.That(GitRepository.PathsEqual(@"C:\proj\", "C:/proj"), Is.True);
            Assert.That(GitRepository.PathsEqual(@"C:\proj", @"C:\proj\Assets"), Is.False);
        }
    }
}
