using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>バリエーション against a real git: two working copies sharing a bare repository on disk.</summary>
    public class VariationRunnerTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _here;
        private TempDirectory _there;
        private TempDirectory _remoteDir;
        private GitRepository _mine;
        private string _remoteUrl;
        private string _main;

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
            _here = new TempDirectory("shiori-here");
            _there = new TempDirectory("shiori-there");
            _remoteDir = new TempDirectory("shiori-remote");
            Run(_remoteDir.Path, "init", "--bare", "-q", _remoteDir.Path);
            _remoteUrl = _remoteDir.Path.Replace('\\', '/');

            _mine = Init(_here);
            Save(_mine, _here, "Assets/a.txt", "v1");
            _main = Status(_mine).Branch;
        }

        [TearDown]
        public void TearDown()
        {
            _here.Dispose();
            _there.Dispose();
            _remoteDir.Dispose();
        }

        private void Run(string dir, params string[] args)
        {
            var result = new ProcessGitRunner().RunAsync(_gitPath, args, dir, None).GetAwaiter().GetResult();
            Assert.That(result.Succeeded, Is.True, result.Stderr);
        }

        private GitRepository Init(TempDirectory dir)
        {
            var repo = new GitRepository(new ProcessGitRunner(), _gitPath, dir.Path);
            repo.InitAsync(None).GetAwaiter().GetResult();
            repo.SetIdentityAsync("Shiori Test", "shiori@example.com", None).GetAwaiter().GetResult();
            repo.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
            return repo;
        }

        private static string Save(GitRepository repo, TempDirectory dir, string file, string text)
        {
            dir.WriteText(file, text);
            repo.AddAllAsync(None).GetAwaiter().GetResult();
            return repo.CommitAsync("save " + file, None).GetAwaiter().GetResult();
        }

        private static WorktreeStatus Status(GitRepository repo) => repo.GetStatusAsync(None).GetAwaiter().GetResult();

        private BranchInfo Branch(string name) => _mine.GetBranchesAsync(None).GetAwaiter().GetResult().Single(b => b.Name == name);

        private CreateOutcome Create(string branch) => VariationRunner.CreateAsync(_mine, branch, None).GetAwaiter().GetResult();

        private SwitchResult Switch(string name, SwitchMode mode, string memo = null) =>
            VariationRunner.SwitchAsync(_mine, Branch(name), mode, memo, None).GetAwaiter().GetResult();

        [Test]
        public void Create_SwitchesToANewBranch_AndKeepsUnsavedChanges()
        {
            _here.WriteText("Assets/a.txt", "unsaved");

            Assert.That(Create("shiori/衣装A"), Is.EqualTo(CreateOutcome.Created));

            var status = Status(_mine);
            Assert.That(status.Branch, Is.EqualTo("shiori/衣装A"));
            Assert.That(status.Changes.Select(c => c.Path), Is.EqualTo(new[] { "Assets/a.txt" }));
            var branches = _mine.GetBranchesAsync(None).GetAwaiter().GetResult();
            Assert.That(branches.Select(b => b.Name), Is.EquivalentTo(new[] { _main, "shiori/衣装A" }));
            Assert.That(branches.Single(b => b.IsCurrent).Name, Is.EqualTo("shiori/衣装A"));
            Assert.That(branches[0].Subject, Is.EqualTo("save Assets/a.txt"));
        }

        [Test]
        public void Create_RefusesBadAndTakenNames()
        {
            Assert.That(Create("shiori/a..b"), Is.EqualTo(CreateOutcome.InvalidName));
            Assert.That(Create(null), Is.EqualTo(CreateOutcome.InvalidName));
            Assert.That(Create("shiori/x"), Is.EqualTo(CreateOutcome.Created));
            Assert.That(Create("shiori/X"), Is.EqualTo(CreateOutcome.AlreadyExists), "names differing only in case collide on Windows");
            Assert.That(Create(_main), Is.EqualTo(CreateOutcome.AlreadyExists));
        }

        [Test]
        public void Switch_ChangesFiles_AndReportsThem()
        {
            Create("shiori/b");
            Save(_mine, _here, "Assets/a.txt", "on b");
            Save(_mine, _here, "Assets/Scene.unity", "scene");

            var result = Switch(_main, SwitchMode.SaveFirst);

            Assert.That(result.Outcome, Is.EqualTo(SwitchOutcome.Switched));
            Assert.That(result.SavedCommitHash, Is.Null, "nothing was unsaved");
            Assert.That(_here.ReadText("Assets/a.txt"), Is.EqualTo("v1"));
            Assert.That(System.IO.File.Exists(_here.File("Assets/Scene.unity")), Is.False);
            Assert.That(result.ChangedPaths, Is.EquivalentTo(new[] { "Assets/a.txt", "Assets/Scene.unity" }));
            Assert.That(result.ChangedScenePaths, Is.EqualTo(new[] { "Assets/Scene.unity" }));
        }

        [Test]
        public void Switch_SaveFirst_SavesIntoTheVariationBeingLeft()
        {
            Create("shiori/b");
            _here.WriteText("Assets/new.txt", "work");

            var result = Switch(_main, SwitchMode.SaveFirst, "メモ");

            Assert.That(result.SavedCommitHash, Is.Not.Null);
            Assert.That(Branch("shiori/b").Hash, Is.EqualTo(result.SavedCommitHash));
            Assert.That(Branch("shiori/b").Subject, Is.EqualTo("メモ"));
            Assert.That(Status(_mine).HasChanges, Is.False);
            Assert.That(System.IO.File.Exists(_here.File("Assets/new.txt")), Is.False);
        }

        [Test]
        public void Switch_Carry_TakesChangesAlong_OrIsBlockedWithoutChangingAnything()
        {
            Create("shiori/b");
            Save(_mine, _here, "Assets/a.txt", "on b");
            Switch(_main, SwitchMode.SaveFirst);

            // A file that is the same in both variations travels.
            _here.WriteText("Assets/other.txt", "carried");
            var carried = Switch("shiori/b", SwitchMode.Carry);
            Assert.That(carried.Outcome, Is.EqualTo(SwitchOutcome.Switched));
            Assert.That(Status(_mine).Branch, Is.EqualTo("shiori/b"));
            Assert.That(_here.ReadText("Assets/other.txt"), Is.EqualTo("carried"));

            // An edit to a file that differs between them cannot travel.
            _here.WriteText("Assets/a.txt", "edited on b");
            var blocked = Switch(_main, SwitchMode.Carry);
            Assert.That(blocked.Outcome, Is.EqualTo(SwitchOutcome.Blocked));
            Assert.That(blocked.BlockingPaths, Is.EqualTo(new[] { "Assets/a.txt" }));
            Assert.That(Status(_mine).Branch, Is.EqualTo("shiori/b"));
            Assert.That(_here.ReadText("Assets/a.txt"), Is.EqualTo("edited on b"));
        }

        [Test]
        public void AVariationMadeOnAnotherPc_CanBeSwitchedTo_AndDeletedEverywhere()
        {
            _mine.SetRemoteUrlAsync(_remoteUrl, None).GetAwaiter().GetResult();
            SendRunner.SendAsync(_mine, None).GetAwaiter().GetResult();
            Run(_there.Path, "clone", "-q", _remoteUrl, _there.Path);
            var other = new GitRepository(new ProcessGitRunner(), _gitPath, _there.Path);
            other.SetIdentityAsync("Other PC", "other@example.com", None).GetAwaiter().GetResult();
            other.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
            Assert.That(VariationRunner.CreateAsync(other, "shiori/夜", None).GetAwaiter().GetResult(), Is.EqualTo(CreateOutcome.Created));
            Save(other, _there, "Assets/night.txt", "night");
            SendRunner.SendAsync(other, None).GetAwaiter().GetResult();

            _mine.FetchAsync(false, None).GetAwaiter().GetResult();
            var remote = Branch("shiori/夜");
            Assert.That(remote.IsRemoteOnly, Is.True);
            Assert.That(remote.RefName, Is.EqualTo("refs/remotes/origin/shiori/夜"));
            Assert.That(VariationRunner.CountOnlyOnAsync(_mine, remote, None).GetAwaiter().GetResult(), Is.EqualTo(1));

            Switch("shiori/夜", SwitchMode.SaveFirst);
            Assert.That(Status(_mine).Branch, Is.EqualTo("shiori/夜"));
            Assert.That(_here.ReadText("Assets/night.txt"), Is.EqualTo("night"));
            Assert.That(Branch("shiori/夜").IsRemoteOnly, Is.False);
            Assert.That(Branch("shiori/夜").IsOnRemote, Is.True);

            Switch(_main, SwitchMode.SaveFirst);
            VariationRunner.DeleteAsync(_mine, Branch("shiori/夜"), true, None).GetAwaiter().GetResult();
            Assert.That(_mine.GetBranchesAsync(None).GetAwaiter().GetResult().Select(b => b.Name), Is.EqualTo(new[] { _main }));
            Assert.That(other.ListRemoteHeadsAsync(_remoteUrl, None).GetAwaiter().GetResult().Keys, Is.EqualTo(new[] { "refs/heads/" + _main }));
        }

        [Test]
        public void Delete_RefusesTheCurrentBranch()
        {
            Assert.Throws<System.ArgumentException>(() => VariationRunner.DeleteAsync(_mine, Branch(_main), false, None).GetAwaiter().GetResult());
        }

        [Test]
        public void Merge_BringsTheOtherBranchIn()
        {
            Create("shiori/b");
            Save(_mine, _here, "Assets/b.txt", "from b");
            Switch(_main, SwitchMode.SaveFirst);
            Save(_mine, _here, "Assets/main.txt", "from main");

            var result = VariationRunner.MergeAsync(_mine, Branch("shiori/b"), None).GetAwaiter().GetResult();

            Assert.That(result.Outcome, Is.EqualTo(MergeOutcome.Merged));
            Assert.That(result.ChangedPaths, Is.EqualTo(new[] { "Assets/b.txt" }));
            Assert.That(_here.ReadText("Assets/b.txt"), Is.EqualTo("from b"));
            Assert.That(VariationRunner.MergeAsync(_mine, Branch("shiori/b"), None).GetAwaiter().GetResult().Outcome, Is.EqualTo(MergeOutcome.UpToDate));
        }

        [Test]
        public void Merge_WithAConflict_IsAbortedAndChangesNothing()
        {
            Create("shiori/b");
            Save(_mine, _here, "Assets/a.txt", "b's version");
            Switch(_main, SwitchMode.SaveFirst);
            var head = Save(_mine, _here, "Assets/a.txt", "main's version");

            var result = VariationRunner.MergeAsync(_mine, Branch("shiori/b"), None).GetAwaiter().GetResult();

            Assert.That(result.Outcome, Is.EqualTo(MergeOutcome.Conflicted));
            Assert.That(result.ConflictPaths, Is.EqualTo(new[] { "Assets/a.txt" }));
            Assert.That(Status(_mine).HasChanges, Is.False);
            Assert.That(_mine.GetHeadAsync(None).GetAwaiter().GetResult(), Is.EqualTo(head));
            Assert.That(_here.ReadText("Assets/a.txt"), Is.EqualTo("main's version"));
        }

        [Test]
        public void Merge_IsNotStartedWithUnsavedChanges()
        {
            Create("shiori/b");
            Save(_mine, _here, "Assets/b.txt", "from b");
            Switch(_main, SwitchMode.SaveFirst);
            _here.WriteText("Assets/a.txt", "unsaved");

            Assert.That(VariationRunner.MergeAsync(_mine, Branch("shiori/b"), None).GetAwaiter().GetResult().Outcome, Is.EqualTo(MergeOutcome.Unsaved));
            Assert.That(System.IO.File.Exists(_here.File("Assets/b.txt")), Is.False);
        }
    }
}
