using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>「取り出す」 against a real git repository: apply only when nothing can conflict, never touch the tree otherwise.</summary>
    public class SetAsideRunnerTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _dir;
        private GitRepository _repo;
        private string _first;

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
            _dir.WriteText("Assets/c.txt", "same in both");
            _first = Save("first");

            _dir.WriteText("Assets/a.txt", "v2");
            _dir.WriteText("Assets/b.txt", "added in second");
            Save("second");
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

        /// <summary>「保存せずに戻す」 to the first snapshot and returns what Shiori set aside.</summary>
        private SetAsideChange RestoreSettingAside()
        {
            var result = RestoreRunner.RunAsync(_repo, _first, "first", RestoreMode.StashFirst, null, None).GetAwaiter().GetResult();
            Assert.That(result.StashHash, Is.Not.Null);
            return SetAsideChange.FromStashList(_repo.StashListAsync(None).GetAwaiter().GetResult()).Single();
        }

        private bool TreeIsClean => !_repo.GetStatusAsync(None).GetAwaiter().GetResult().HasChanges;

        [Test]
        public void SetAside_RecordsTheRestoreTargetAndTime()
        {
            _dir.WriteText("Assets/c.txt", "edited");
            var change = RestoreSettingAside();

            Assert.That(change.RestoreTarget, Is.EqualTo("first"));
            Assert.That(change.Stash.Message, Does.Contain(SetAsideChange.Marker + ": first"));
            Assert.That((System.DateTimeOffset.UtcNow - change.Time).TotalMinutes, Is.LessThan(5));
        }

        [Test]
        public void DisjointChanges_AreBroughtBackAndTheStashIsKept()
        {
            _dir.WriteText("Assets/c.txt", "edited");
            _dir.WriteText("Assets/new.txt", "never saved");
            var change = RestoreSettingAside();
            Assert.That(_dir.ReadText("Assets/c.txt"), Is.EqualTo("same in both"));
            Assert.That(System.IO.File.Exists(_dir.File("Assets/new.txt")), Is.False);

            var plan = SetAsideRunner.ApplyAsync(_repo, change.Stash, None).GetAwaiter().GetResult();

            Assert.That(plan.Block, Is.EqualTo(SetAsideBlock.None));
            Assert.That(plan.Paths, Is.EqualTo(new[] { "Assets/c.txt", "Assets/new.txt" }));
            Assert.That(_dir.ReadText("Assets/c.txt"), Is.EqualTo("edited"));
            Assert.That(_dir.ReadText("Assets/new.txt"), Is.EqualTo("never saved"));
            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("v1"), "the restored files stay as they were restored");
            Assert.That(_repo.StashListAsync(None).GetAwaiter().GetResult().Count, Is.EqualTo(1), "取り出す never deletes the set-aside changes");
        }

        [Test]
        public void OverlappingChanges_AreRefusedAndNothingIsTouched()
        {
            _dir.WriteText("Assets/a.txt", "edited after second");
            _dir.WriteText("Assets/c.txt", "edited");
            var change = RestoreSettingAside();

            var plan = SetAsideRunner.ApplyAsync(_repo, change.Stash, None).GetAwaiter().GetResult();

            Assert.That(plan.Block, Is.EqualTo(SetAsideBlock.Overlap));
            Assert.That(plan.OverlappingPaths, Is.EqualTo(new[] { "Assets/a.txt" }));
            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("v1"));
            Assert.That(_dir.ReadText("Assets/c.txt"), Is.EqualTo("same in both"));
            Assert.That(TreeIsClean, Is.True);
        }

        [Test]
        public void NewFileThatNowExistsInHistory_CountsAsOverlap()
        {
            // An unsaved new file is set aside, and later a different file of the same name is saved.
            _dir.WriteText("Assets/d.txt", "unsaved new file");
            var change = RestoreSettingAside();
            _dir.WriteText("Assets/d.txt", "saved under the same name");
            Save("d saved");

            var plan = SetAsideRunner.PlanAsync(_repo, change.Stash, None).GetAwaiter().GetResult();

            Assert.That(plan.Block, Is.EqualTo(SetAsideBlock.Overlap));
            Assert.That(plan.OverlappingPaths, Is.EqualTo(new[] { "Assets/d.txt" }));
        }

        [Test]
        public void UnsavedWork_BlocksBringingBack()
        {
            _dir.WriteText("Assets/c.txt", "edited");
            var change = RestoreSettingAside();
            _dir.WriteText("Assets/other.txt", "new work after the restore");

            var plan = SetAsideRunner.ApplyAsync(_repo, change.Stash, None).GetAwaiter().GetResult();

            Assert.That(plan.Block, Is.EqualTo(SetAsideBlock.WorkingTreeHasChanges));
            Assert.That(_dir.ReadText("Assets/c.txt"), Is.EqualTo("same in both"), "nothing was applied");
        }

        [Test]
        public void FromStashList_IgnoresStashesNotMadeByShiori()
        {
            var list = new[]
            {
                new StashEntry("stash@{0}", "a", "On main: my own work"),
                new StashEntry("stash@{1}", "b", "On main: " + SetAsideChange.Marker + ": 衣装を追加"),
                new StashEntry("stash@{2}", "c", "On main: " + SetAsideChange.Marker),
            };
            var changes = SetAsideChange.FromStashList(list);
            Assert.That(changes.Select(c => c.Stash.Hash), Is.EqualTo(new[] { "b", "c" }));
            Assert.That(changes[0].RestoreTarget, Is.EqualTo("衣装を追加"));
            Assert.That(changes[1].RestoreTarget, Is.Empty, "stashes from before 0.2 have no target");
            Assert.That(SetAsideChange.MessageFor("two\nlines"), Is.EqualTo(SetAsideChange.Marker + ": two lines"));
            Assert.That(SetAsideChange.MessageFor(null), Is.EqualTo(SetAsideChange.Marker));
        }
    }
}
