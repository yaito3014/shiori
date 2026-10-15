using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>The Git LFS setup against a real git with git-lfs, plus the states a fake git can reach.</summary>
    public class LargeFileSetupTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _dir;
        private GitRepository _repo;

        [OneTimeSetUp]
        public void LocateGit()
        {
            var location = new GitLocator(new ProcessGitRunner()).LocateAsync(null, None).GetAwaiter().GetResult();
            Assume.That(location.Found && location.MeetsMinimumVersion, Is.True, "a supported git is required");
            _gitPath = location.Path;
        }

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDirectory("shiori-lfs");
            _repo = new GitRepository(new ProcessGitRunner(), _gitPath, _dir.Path);
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
        }

        private void RequireLfs()
        {
            Assume.That(_repo.HasLfsAsync(None).GetAwaiter().GetResult(), Is.True, "git-lfs is required");
        }

        private void InitRepository()
        {
            _repo.InitAsync(None).GetAwaiter().GetResult();
            _repo.SetIdentityAsync("Shiori Test", "shiori@example.com", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
        }

        private LargeFileStatus Evaluate(bool skipped = false)
        {
            return LargeFileSetup.EvaluateAsync(_repo, skipped, None).GetAwaiter().GetResult();
        }

        private string Git(params string[] args)
        {
            var result = new ProcessGitRunner().RunAsync(_gitPath, args, _dir.Path, None).GetAwaiter().GetResult();
            Assert.That(result.Succeeded, Is.True, result.Stderr);
            return result.Stdout;
        }

        [Test]
        public void UsesLfs_LooksForTheFilterOutsideComments()
        {
            Assert.That(LargeFileSetup.UsesLfs(null), Is.False);
            Assert.That(LargeFileSetup.UsesLfs("*.cs text\n# *.png filter=lfs\n"), Is.False);
            Assert.That(LargeFileSetup.UsesLfs("*.cs text\r\n*.png  filter=lfs diff=lfs merge=lfs -text\r\n"), Is.True);
            Assert.That(LargeFileSetup.UsesLfs("*.png filter=lfsx\n"), Is.False);
        }

        [Test]
        public void Attributes_CoverUpperCaseExtensions()
        {
            Assert.That(LargeFileSetup.GitAttributes, Does.Contain("*.png filter=lfs diff=lfs merge=lfs -text"));
            Assert.That(LargeFileSetup.GitAttributes, Does.Contain("*.PNG filter=lfs diff=lfs merge=lfs -text"));
            Assert.That(LargeFileSetup.Extensions.Intersect(new[] { "unity", "prefab", "asset", "mat", "meta" }).Any(),
                Is.False, "Unity's text assets stay in git so they can be merged");
        }

        [Test]
        public void Apply_SendsNewSavesThroughLfs()
        {
            RequireLfs();
            InitRepository();
            Assert.That(Evaluate().State, Is.EqualTo(LargeFileState.NotSetUp));
            Assert.That(Evaluate(skipped: true).State, Is.EqualTo(LargeFileState.Skipped));

            LargeFileSetup.ApplyAsync(_repo, None).GetAwaiter().GetResult();
            var status = Evaluate();
            Assert.That(status.State, Is.EqualTo(LargeFileState.Ready));
            Assert.That(status.UsedByProject, Is.True);
            Assert.That(Evaluate(skipped: true).State, Is.EqualTo(LargeFileState.Ready), "a project using LFS is never treated as skipped");

            _dir.WriteText("Assets/a.png", "not really a png");
            _repo.AddAllAsync(None).GetAwaiter().GetResult();
            _repo.CommitAsync("save", None).GetAwaiter().GetResult();
            Assert.That(Git("cat-file", "-p", "HEAD:Assets/a.png"), Does.StartWith("version https://git-lfs.github.com/spec/v1"));
            Assert.That(_dir.ReadText("Assets/a.png"), Is.EqualTo("not really a png"), "the working tree keeps the real file");
        }

        [Test]
        public void Apply_CreatesTheRepositoryWhenThereIsNone()
        {
            RequireLfs();
            LargeFileSetup.ApplyAsync(_repo, None).GetAwaiter().GetResult();
            Assert.That(Directory.Exists(Path.Combine(_dir.Path, ".git")), Is.True);
            Assert.That(_dir.ReadText(".gitattributes"), Does.Contain("*.fbx filter=lfs"));
        }

        [Test]
        public void MissingHook_NeedsInstall_AndBlocks()
        {
            RequireLfs();
            InitRepository();
            LargeFileSetup.ApplyAsync(_repo, None).GetAwaiter().GetResult();
            File.Delete(_repo.GetGitPathAsync("hooks/pre-push", None).GetAwaiter().GetResult());

            var status = Evaluate();
            Assert.That(status.State, Is.EqualTo(LargeFileState.NeedsInstall));
            Assert.That(status.Blocked, Is.True, "this PC would not upload LFS files on 送信");

            LargeFileSetup.InstallAndPullAsync(_repo, None).GetAwaiter().GetResult();
            Assert.That(Evaluate().State, Is.EqualTo(LargeFileState.Ready));
        }

        [Test]
        public void UsersOwnLfsLines_CountAsReady_AndChangedBlockIsOutdated()
        {
            RequireLfs();
            InitRepository();
            _repo.InstallLfsAsync(None).GetAwaiter().GetResult();
            _dir.WriteText(".gitattributes", "*.blend filter=lfs diff=lfs merge=lfs -text\n");
            Assert.That(Evaluate().State, Is.EqualTo(LargeFileState.Ready), "the user's own setup is respected");

            LargeFileSetup.ApplyAsync(_repo, None).GetAwaiter().GetResult();
            File.WriteAllText(_dir.File(".gitattributes"), _dir.ReadText(".gitattributes").Replace("*.wav filter=lfs diff=lfs merge=lfs -text\n", ""));
            Assert.That(Evaluate().State, Is.EqualTo(LargeFileState.Outdated));
        }

        [Test]
        public void WithoutGitLfs_OnlyAProjectUsingItIsBlocked()
        {
            var runner = new FakeGitRunner();
            runner.On("rev-parse --show-toplevel", _dir.Path.Replace('\\', '/') + "\n");
            runner.On("lfs version", "", exitCode: 1, stderr: "git: 'lfs' is not a git command.");
            var repo = new GitRepository(runner, "git", _dir.Path);

            var plain = LargeFileSetup.EvaluateAsync(repo, false, None).GetAwaiter().GetResult();
            Assert.That(plain.State, Is.EqualTo(LargeFileState.ToolMissing));
            Assert.That(plain.Blocked, Is.False);
            Assert.That(LargeFileSetup.EvaluateAsync(repo, true, None).GetAwaiter().GetResult().State, Is.EqualTo(LargeFileState.Skipped));

            _dir.WriteText(".gitattributes", "*.psd filter=lfs diff=lfs merge=lfs -text\n");
            var used = LargeFileSetup.EvaluateAsync(repo, true, None).GetAwaiter().GetResult();
            Assert.That(used.State, Is.EqualTo(LargeFileState.ToolMissing));
            Assert.That(used.Blocked, Is.True);
        }

        [Test]
        public void LfsCommands_AreExact()
        {
            var runner = new FakeGitRunner();
            var repo = new GitRepository(runner, "git", _dir.Path);
            repo.InstallLfsAsync(None).GetAwaiter().GetResult();
            repo.PullLfsAsync(None).GetAwaiter().GetResult();
            repo.GetConfigAsync("filter.lfs.process", None).GetAwaiter().GetResult();
            runner.On("rev-parse --git-path hooks/pre-push", ".git/hooks/pre-push\n");
            Assert.That(repo.GetGitPathAsync("hooks/pre-push", None).GetAwaiter().GetResult(),
                Is.EqualTo(Path.GetFullPath(Path.Combine(_dir.Path, ".git", "hooks", "pre-push"))));

            Assert.That(runner.Calls.Select(c => c.Joined), Is.EqualTo(new[]
            {
                "lfs install --local",
                "lfs pull",
                "config --get filter.lfs.process",
                "rev-parse --git-path hooks/pre-push",
            }));
        }
    }
}
