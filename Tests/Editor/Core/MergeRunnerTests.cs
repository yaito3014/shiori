using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>Merges that stop on conflicts, and the merge-driver setup, against a real git.</summary>
    public class MergeRunnerTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _dir;
        private GitRepository _repo;
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
            _dir = new TempDirectory("shiori-merge");
            _repo = new GitRepository(new ProcessGitRunner(), _gitPath, _dir.Path);
            _repo.InitAsync(None).GetAwaiter().GetResult();
            _repo.SetIdentityAsync("Shiori Test", "shiori@example.com", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
            Save("Assets/a.txt", "base");
            _main = _repo.GetStatusAsync(None).GetAwaiter().GetResult().Branch;
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
        }

        private string Save(string file, string text)
        {
            _dir.WriteText(file, text);
            _repo.AddAllAsync(None).GetAwaiter().GetResult();
            return _repo.CommitAsync("save " + file, None).GetAwaiter().GetResult();
        }

        private void Delete(string file)
        {
            File.Delete(_dir.File(file));
            _repo.AddAllAsync(None).GetAwaiter().GetResult();
            _repo.CommitAsync("delete " + file, None).GetAwaiter().GetResult();
        }

        /// <summary>Makes branch "other" with <paramref name="onOther"/>, then <paramref name="onMain"/> on main, and merges other in.</summary>
        private System.Collections.Generic.IReadOnlyList<ConflictEntry> MergeAfter(System.Action onOther, System.Action onMain)
        {
            _repo.CreateBranchAsync("other", None).GetAwaiter().GetResult();
            onOther();
            _repo.SwitchBranchAsync(_main, false, None).GetAwaiter().GetResult();
            onMain();
            return MergeRunner.StartAsync(_repo, "other", null, None).GetAwaiter().GetResult();
        }

        [Test]
        public void ConflictParser_GroupsStagesByPath()
        {
            var text = "100644 aaa 1\tAssets/a.txt\0100644 bbb 2\tAssets/a.txt\0100644 ccc 3\tAssets/a.txt\0"
                       + "100644 ddd 1\tAssets/gone b.mat\0100644 eee 3\tAssets/gone b.mat\0";
            var entries = ConflictParser.Parse(text);
            Assert.That(entries.Select(e => e.Path), Is.EqualTo(new[] { "Assets/a.txt", "Assets/gone b.mat" }));
            Assert.That(entries[0].BaseHash + entries[0].OursHash + entries[0].TheirsHash, Is.EqualTo("aaabbbccc"));
            Assert.That(entries[1].IsDeletedOn(ConflictSide.Ours), Is.True);
            Assert.That(entries[1].IsDeletedOn(ConflictSide.Theirs), Is.False);
            Assert.That(ConflictParser.Parse(string.Empty), Is.Empty);
        }

        [Test]
        public void CleanMerge_FinishesAtOnce()
        {
            var conflicts = MergeAfter(() => Save("Assets/b.txt", "other"), () => Save("Assets/c.txt", "main"));
            Assert.That(conflicts, Is.Empty);
            Assert.That(MergeRunner.GetStateAsync(_repo, None).GetAwaiter().GetResult().InProgress, Is.False);
            Assert.That(_dir.ReadText("Assets/b.txt"), Is.EqualTo("other"));
        }

        [Test]
        public void Conflict_ShowsOursAndWaits_ThenOursIsKept()
        {
            var conflicts = MergeAfter(() => Save("Assets/a.txt", "theirs"), () => Save("Assets/a.txt", "ours"));

            Assert.That(conflicts.Select(c => c.Path), Is.EqualTo(new[] { "Assets/a.txt" }));
            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("ours"));
            Assert.That(MergeRunner.FindEditedAsync(_repo, conflicts, None).GetAwaiter().GetResult(), Is.Empty, "nothing edited since the stop");
            Assert.That(MergeRunner.FinishAsync(_repo, None).GetAwaiter().GetResult(), Is.Null, "cannot finish while undecided");

            MergeRunner.ResolveAsync(_repo, conflicts, ConflictSide.Ours, None).GetAwaiter().GetResult();
            var state = MergeRunner.GetStateAsync(_repo, None).GetAwaiter().GetResult();
            Assert.That(state.ReadyToFinish, Is.True);
            Assert.That(MergeRunner.FinishAsync(_repo, None).GetAwaiter().GetResult(), Is.Not.Null);
            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("ours"));
            Assert.That(_repo.GetStatusAsync(None).GetAwaiter().GetResult().HasChanges, Is.False);
        }

        [Test]
        public void EditsMadeAfterTheStop_AreFoundBeforeTheyAreOverwritten()
        {
            var conflicts = MergeAfter(() => Save("Assets/a.txt", "theirs"), () => Save("Assets/a.txt", "ours"));
            _dir.WriteText("Assets/a.txt", "edited in Unity meanwhile");
            Assert.That(MergeRunner.FindEditedAsync(_repo, conflicts, None).GetAwaiter().GetResult(), Is.EqualTo(new[] { "Assets/a.txt" }));
        }

        [Test]
        public void DeletedOnOneSide_ChoosingThatSideDeletesTheFile()
        {
            Save("Assets/d.txt", "base");
            var conflicts = MergeAfter(() => Save("Assets/d.txt", "changed there"), () => Delete("Assets/d.txt"));

            var entry = conflicts.Single(c => c.Path == "Assets/d.txt");
            Assert.That(entry.IsDeletedOn(ConflictSide.Ours), Is.True);
            Assert.That(_dir.ReadText("Assets/d.txt"), Is.EqualTo("changed there"), "git leaves their version while undecided");
            Assert.That(MergeRunner.FindEditedAsync(_repo, conflicts, None).GetAwaiter().GetResult(), Is.Empty);

            MergeRunner.ResolveAsync(_repo, conflicts, ConflictSide.Ours, None).GetAwaiter().GetResult();
            Assert.That(File.Exists(_dir.File("Assets/d.txt")), Is.False);
            Assert.That(MergeRunner.FinishAsync(_repo, None).GetAwaiter().GetResult(), Is.Not.Null);
        }

        [Test]
        public void RefusedMerge_ThrowsAndStartsNothing()
        {
            _repo.CreateBranchAsync("other", None).GetAwaiter().GetResult();
            Save("Assets/new.txt", "tracked there");
            _repo.SwitchBranchAsync(_main, false, None).GetAwaiter().GetResult();
            Save("Assets/c.txt", "main");
            _dir.WriteText("Assets/new.txt", "untracked here");

            Assert.Throws<GitException>(() => MergeRunner.StartAsync(_repo, "other", null, None).GetAwaiter().GetResult());
            Assert.That(MergeRunner.GetStateAsync(_repo, None).GetAwaiter().GetResult().InProgress, Is.False);
            Assert.That(_dir.ReadText("Assets/new.txt"), Is.EqualTo("untracked here"));
        }

        [Test]
        public void DriverSetup_WritesConfigAndBlock_AndNoticesAnotherEditor()
        {
            using (var tools = new TempDirectory("shiori-tool"))
            {
                tools.WriteText("UnityYAMLMerge.exe", "stand-in");
                var tool = tools.File("UnityYAMLMerge.exe");
                _dir.WriteText(".gitattributes", "*.png binary\n");

                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tool, None).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Missing));
                MergeDriverSetup.ApplyAsync(_repo, tool, None).GetAwaiter().GetResult();

                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tool, None).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Ready));
                var driver = _repo.GetLocalConfigAsync(MergeDriverSetup.DriverKey, None).GetAwaiter().GetResult();
                Assert.That(driver, Does.StartWith("\"" + tool.Replace('\\', '/') + "\" merge -h --force --fallback none"));
                var attributes = _dir.ReadText(".gitattributes");
                Assert.That(attributes, Does.StartWith("*.png binary"), "the user's lines stay");
                Assert.That(attributes, Does.Contain("*.prefab merge=unityyamlmerge"));

                tools.WriteText("Other/UnityYAMLMerge.exe", "another editor");
                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tools.File("Other/UnityYAMLMerge.exe"), None).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Outdated));
                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tools.File("missing.exe"), None).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.ToolMissing));
            }
        }

        [Test]
        public void DriverSetup_CanBeTurnedOff_AndSkippedOnlyWithoutTheBlock()
        {
            using (var tools = new TempDirectory("shiori-tool"))
            {
                tools.WriteText("UnityYAMLMerge.exe", "stand-in");
                var tool = tools.File("UnityYAMLMerge.exe");
                _dir.WriteText(".gitattributes", "*.png binary\n");
                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tool, None, skipped: true).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Skipped));

                MergeDriverSetup.ApplyAsync(_repo, tool, None).GetAwaiter().GetResult();
                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tool, None, skipped: true).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Ready),
                    "a block in the project wins over an old choice");

                MergeDriverSetup.DisableAsync(_repo, None).GetAwaiter().GetResult();
                Assert.That(_repo.GetLocalConfigAsync(MergeDriverSetup.DriverKey, None).GetAwaiter().GetResult(), Is.Null);
                Assert.That(_repo.GetLocalConfigAsync(MergeDriverSetup.NameKey, None).GetAwaiter().GetResult(), Is.Null);
                Assert.That(_dir.ReadText(".gitattributes").TrimEnd('\r', '\n'), Is.EqualTo("*.png binary"), "only Shiori's block goes");
                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tool, None, skipped: true).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Skipped));
                Assert.That(MergeDriverSetup.EvaluateAsync(_repo, tool, None).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Missing));
                Assert.DoesNotThrow(() => MergeDriverSetup.DisableAsync(_repo, None).GetAwaiter().GetResult(), "turning off twice is harmless");
            }
        }

        [Test]
        public void DriverSetup_BeforeTheFirstSave_CreatesTheRepository()
        {
            using (var fresh = new TempDirectory("shiori-fresh"))
            using (var tools = new TempDirectory("shiori-tool"))
            {
                tools.WriteText("UnityYAMLMerge.exe", "stand-in");
                var repo = new GitRepository(new ProcessGitRunner(), _gitPath, fresh.Path);
                Assert.That(MergeDriverSetup.EvaluateAsync(repo, tools.File("UnityYAMLMerge.exe"), None).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Missing));
                MergeDriverSetup.ApplyAsync(repo, tools.File("UnityYAMLMerge.exe"), None).GetAwaiter().GetResult();
                Assert.That(repo.ProbeAsync(None).GetAwaiter().GetResult().State, Is.EqualTo(RepositoryState.Ready));
                Assert.That(repo.GetHeadAsync(None).GetAwaiter().GetResult(), Is.Null, "nothing is saved by the setup itself");
                Assert.That(MergeDriverSetup.EvaluateAsync(repo, tools.File("UnityYAMLMerge.exe"), None).GetAwaiter().GetResult(), Is.EqualTo(MergeDriverState.Ready));
            }
        }
    }
}
