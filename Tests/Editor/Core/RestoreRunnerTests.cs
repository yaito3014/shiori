using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>Runs the restore sequence against a real git repository in a temporary directory.</summary>
    public class RestoreRunnerTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _dir;
        private GitRepository _repo;
        private string _first;
        private string _second;

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
            _dir = new TempDirectory();
            _repo = new GitRepository(new ProcessGitRunner(), _gitPath, _dir.Path);
            _repo.InitAsync(None).GetAwaiter().GetResult();
            _repo.SetIdentityAsync("Shiori Test", "shiori@example.com", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();

            _dir.WriteText("Assets/a.txt", "v1");
            _dir.WriteText("Assets/gone.txt", "will be deleted in second");
            _dir.WriteText("ProjectSettings/ProjectSettings.asset", "settings v1");
            _first = Save("first");

            _dir.WriteText("Assets/a.txt", "v2");
            System.IO.File.Delete(_dir.File("Assets/gone.txt"));
            _dir.WriteText("Assets/b.txt", "added in second");
            _dir.WriteText("ProjectSettings/ProjectSettings.asset", "settings v2");
            _second = Save("second");
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
        }

        private string Save(string message)
        {
            _repo.AddAllAsync(None).GetAwaiter().GetResult();
            return _repo.CommitAsync(message, None).GetAwaiter().GetResult();
        }

        private RestoreResult Restore(RestoreMode mode, string memo = null)
        {
            return RestoreRunner.RunAsync(_repo, _first, "first", mode, memo, None).GetAwaiter().GetResult();
        }

        private void AssertTreeMatchesFirst()
        {
            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("v1"));
            Assert.That(_dir.ReadText("Assets/gone.txt"), Is.EqualTo("will be deleted in second"));
            Assert.That(System.IO.File.Exists(_dir.File("Assets/b.txt")), Is.False);
            Assert.That(_dir.ReadText("ProjectSettings/ProjectSettings.asset"), Is.EqualTo("settings v1"));
            Assert.That(_repo.GetStatusAsync(None).GetAwaiter().GetResult().HasChanges, Is.False);

            var diff = new ProcessGitRunner().RunAsync(_gitPath, new[] { "diff", "--quiet", _first }, _dir.Path, None).GetAwaiter().GetResult();
            Assert.That(diff.ExitCode, Is.EqualTo(0), "git diff <first> must be empty after restoring");
        }

        [Test]
        public void CleanTree_RestoresAndAddsOneRestoreCommit()
        {
            var result = Restore(RestoreMode.StashFirst);

            AssertTreeMatchesFirst();
            Assert.That(result.ChangedAnything, Is.True);
            Assert.That(result.StashHash, Is.Null, "nothing to stash on a clean tree");
            Assert.That(result.SavedCommitHash, Is.Null);
            Assert.That(result.TouchedProjectSettings, Is.True);
            Assert.That(result.Changes.Select(c => c.Path), Is.EquivalentTo(new[] { "Assets/a.txt", "Assets/gone.txt", "Assets/b.txt", "ProjectSettings/ProjectSettings.asset" }));

            var log = _repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult();
            Assert.That(log.Select(s => s.Hash), Is.EqualTo(new[] { result.RestoreCommitHash, _second, _first }), "history stays linear");
            Assert.That(log[0].Message, Is.EqualTo("Restore: first"));
            Assert.That(_repo.StashListAsync(None).GetAwaiter().GetResult(), Is.Empty);
        }

        [Test]
        public void DirtyTree_StashFirst_KeepsChangesInStash()
        {
            _dir.WriteText("Assets/a.txt", "unsaved edit");
            _dir.WriteText("Assets/untracked.txt", "never saved");

            var result = Restore(RestoreMode.StashFirst);

            AssertTreeMatchesFirst();
            Assert.That(result.StashHash, Has.Length.EqualTo(40));
            var stashes = _repo.StashListAsync(None).GetAwaiter().GetResult();
            Assert.That(stashes.Count, Is.EqualTo(1));
            Assert.That(stashes[0].Hash, Is.EqualTo(result.StashHash));
            Assert.That(stashes[0].Message, Does.Contain(RestoreRunner.AutoStashMessage));

            var log = _repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult();
            Assert.That(log.Count, Is.EqualTo(3), "no extra snapshot commit in StashFirst mode");
        }

        [Test]
        public void DirtyTree_SaveFirst_CommitsThenRestores()
        {
            _dir.WriteText("Assets/a.txt", "unsaved edit");
            _dir.WriteText("Assets/untracked.txt", "never saved");

            var result = Restore(RestoreMode.SaveFirst, "着せ替え前");

            AssertTreeMatchesFirst();
            Assert.That(result.SavedCommitHash, Has.Length.EqualTo(40));
            Assert.That(result.StashHash, Is.Null);

            var log = _repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult();
            Assert.That(log.Select(s => s.Hash), Is.EqualTo(new[] { result.RestoreCommitHash, result.SavedCommitHash, _second, _first }));
            Assert.That(log[1].Message, Is.EqualTo("着せ替え前"));
            Assert.That(log[1].Changes.Select(c => c.Path), Is.EquivalentTo(new[] { "Assets/a.txt", "Assets/untracked.txt" }));
            Assert.That(_repo.StashListAsync(None).GetAwaiter().GetResult(), Is.Empty);
        }

        [Test]
        public void SaveFirst_WithoutMemo_UsesGeneratedMessage()
        {
            _dir.WriteText("Assets/a.txt", "unsaved edit");
            var result = Restore(RestoreMode.SaveFirst, "   ");
            var saved = _repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult().Single(s => s.Hash == result.SavedCommitHash);
            Assert.That(saved.Message, Is.EqualTo("Snapshot: 1 files changed (0 added, 1 modified, 0 deleted)"));
        }

        [Test]
        public void RestoringToHead_ChangesNothing()
        {
            var result = RestoreRunner.RunAsync(_repo, _second, "second", RestoreMode.StashFirst, null, None).GetAwaiter().GetResult();
            Assert.That(result.ChangedAnything, Is.False);
            Assert.That(result.Changes, Is.Empty);
            Assert.That(_repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult().Count, Is.EqualTo(2));
        }

        [Test]
        public void RestoringToHead_WithDirtyTree_StashesAndRevertsToHead()
        {
            _dir.WriteText("Assets/a.txt", "unsaved edit");
            var result = RestoreRunner.RunAsync(_repo, _second, "second", RestoreMode.StashFirst, null, None).GetAwaiter().GetResult();
            Assert.That(result.ChangedAnything, Is.False);
            Assert.That(result.StashHash, Is.Not.Null);
            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("v2"));
        }

        [Test]
        public void RestoreTwice_IsIdempotentAndKeepsHistoryLinear()
        {
            var one = Restore(RestoreMode.StashFirst);
            var two = Restore(RestoreMode.StashFirst);
            Assert.That(one.ChangedAnything, Is.True);
            Assert.That(two.ChangedAnything, Is.False);
            Assert.That(_repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult().Count, Is.EqualTo(3));
        }
    }
}
