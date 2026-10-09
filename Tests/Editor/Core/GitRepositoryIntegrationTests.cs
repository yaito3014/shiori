using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>Exercises <see cref="GitRepository"/> against a real git in a temporary repository.</summary>
    public class GitRepositoryIntegrationTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _dir;
        private GitRepository _repo;

        [OneTimeSetUp]
        public void LocateGit()
        {
            var location = new GitLocator(new ProcessGitRunner()).LocateAsync(null, None).GetAwaiter().GetResult();
            Assume.That(location.Found, Is.True, "git was not found on this machine; integration tests are skipped");
            Assume.That(location.MeetsMinimumVersion, Is.True, "git " + location.VersionText + " is older than the supported minimum");
            _gitPath = location.Path;
        }

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDirectory();
            _repo = new GitRepository(new ProcessGitRunner(), _gitPath, _dir.Path);
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
        }

        private void InitWithIdentity()
        {
            _repo.InitAsync(None).GetAwaiter().GetResult();
            _repo.SetIdentityAsync("Shiori Test", "shiori@example.com", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
        }

        private string Save(string message)
        {
            _repo.AddAllAsync(None).GetAwaiter().GetResult();
            return _repo.CommitAsync(message, None).GetAwaiter().GetResult();
        }

        [Test]
        public void ReadFileAt_ReturnsTheTextOfThatSnapshot_OrNullWhenAbsent()
        {
            InitWithIdentity();
            _dir.WriteText("Packages/vpm-manifest.json", "{ \"locked\": { \"com.vrchat.base\": {} } }");
            _dir.WriteText("Assets/日本語 ファイル.txt", "こんにちは");
            var first = Save("first");
            _dir.WriteText("Packages/vpm-manifest.json", "{ \"locked\": {} }");
            Save("second");

            Assert.That(_repo.ReadFileAtAsync(first, "Packages/vpm-manifest.json", None).GetAwaiter().GetResult(), Is.EqualTo("{ \"locked\": { \"com.vrchat.base\": {} } }"));
            Assert.That(_repo.ReadFileAtAsync("HEAD", "Packages/vpm-manifest.json", None).GetAwaiter().GetResult(), Is.EqualTo("{ \"locked\": {} }"));
            Assert.That(_repo.ReadFileAtAsync(first, "Assets\\日本語 ファイル.txt", None).GetAwaiter().GetResult(), Is.EqualTo("こんにちは"));
            Assert.That(_repo.ReadFileAtAsync(first, "Packages/missing.json", None).GetAwaiter().GetResult(), Is.Null);
            Assert.That(_repo.ReadFileAtAsync(first, "Packages", None).GetAwaiter().GetResult(), Is.Null, "a folder is not a file");
        }

        [Test]
        public void Probe_BeforeAndAfterInit()
        {
            Assert.That(_repo.ProbeAsync(None).GetAwaiter().GetResult().State, Is.EqualTo(RepositoryState.NotARepository));
            _repo.InitAsync(None).GetAwaiter().GetResult();
            var probe = _repo.ProbeAsync(None).GetAwaiter().GetResult();
            Assert.That(probe.State, Is.EqualTo(RepositoryState.Ready));
            Assert.That(GitRepository.PathsEqual(probe.TopLevel, _dir.Path), Is.True);
        }

        [Test]
        public void Probe_SubdirectoryOfRepositoryIsRootMismatch()
        {
            _repo.InitAsync(None).GetAwaiter().GetResult();
            _dir.WriteText("Assets/x.txt", "x");
            var sub = new GitRepository(new ProcessGitRunner(), _gitPath, _dir.File("Assets"));
            var probe = sub.ProbeAsync(None).GetAwaiter().GetResult();
            Assert.That(probe.State, Is.EqualTo(RepositoryState.RootMismatch));
        }

        [Test]
        public void Init_IsIdempotent()
        {
            _repo.InitAsync(None).GetAwaiter().GetResult();
            Assert.DoesNotThrow(() => _repo.InitAsync(None).GetAwaiter().GetResult());
        }

        [Test]
        public void EmptyRepository_HasNoHeadNoLogNoChanges()
        {
            InitWithIdentity();
            Assert.That(_repo.GetHeadAsync(None).GetAwaiter().GetResult(), Is.Null);
            Assert.That(_repo.GetLogAsync(200, 0, None).GetAwaiter().GetResult(), Is.Empty);
            var status = _repo.GetStatusAsync(None).GetAwaiter().GetResult();
            Assert.That(status.HasChanges, Is.False);
            Assert.That(status.HeadHash, Is.Null);
        }

        [Test]
        public void Identity_RoundTrips()
        {
            InitWithIdentity();
            var identity = _repo.GetIdentityAsync(None).GetAwaiter().GetResult();
            Assert.That(identity.Name, Is.EqualTo("Shiori Test"));
            Assert.That(identity.Email, Is.EqualTo("shiori@example.com"));
            Assert.That(identity.IsComplete, Is.True);
        }

        [Test]
        public void Status_Add_Commit_Log_RoundTrip()
        {
            InitWithIdentity();
            _dir.WriteText("Assets/a b.txt", "one");
            _dir.WriteText("Assets/日本語.txt", "二");
            _dir.WriteText("Assets/日本語.txt.meta", "meta");

            var status = _repo.GetStatusAsync(None).GetAwaiter().GetResult();
            Assert.That(status.Changes.Select(c => c.Path), Is.EquivalentTo(new[] { "Assets/a b.txt", "Assets/日本語.txt", "Assets/日本語.txt.meta" }));
            Assert.That(status.Changes.All(c => c.Kind == ChangeKind.Untracked), Is.True);
            Assert.That(status.Stats.Added, Is.EqualTo(3));

            var first = Save("Initial snapshot (Shiori)");
            Assert.That(first, Has.Length.EqualTo(40));
            Assert.That(_repo.GetHeadAsync(None).GetAwaiter().GetResult(), Is.EqualTo(first));
            Assert.That(_repo.GetStatusAsync(None).GetAwaiter().GetResult().HasChanges, Is.False);

            _dir.WriteText("Assets/a b.txt", "changed");
            System.IO.File.Delete(_dir.File("Assets/日本語.txt.meta"));
            _dir.WriteText("Assets/new.txt", "new");

            status = _repo.GetStatusAsync(None).GetAwaiter().GetResult();
            Assert.That(status.HeadHash, Is.EqualTo(first));
            Assert.That(status.Changes.Single(c => c.Path == "Assets/a b.txt").Kind, Is.EqualTo(ChangeKind.Modified));
            Assert.That(status.Changes.Single(c => c.Path == "Assets/日本語.txt.meta").Kind, Is.EqualTo(ChangeKind.Deleted));
            Assert.That(status.Changes.Single(c => c.Path == "Assets/new.txt").Kind, Is.EqualTo(ChangeKind.Untracked));

            var second = Save("衣装を着せ替える前");

            var log = _repo.GetLogAsync(200, 0, None).GetAwaiter().GetResult();
            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log[0].Hash, Is.EqualTo(second));
            Assert.That(log[0].Message, Is.EqualTo("衣装を着せ替える前"));
            Assert.That(log[0].Author, Is.EqualTo("Shiori Test"));
            Assert.That(log[0].Stats.Added, Is.EqualTo(1));
            Assert.That(log[0].Stats.Modified, Is.EqualTo(1));
            Assert.That(log[0].Stats.Deleted, Is.EqualTo(1));
            Assert.That(log[1].Hash, Is.EqualTo(first));
            Assert.That(log[1].Changes.Count, Is.EqualTo(3));
            Assert.That(log[1].Changes.All(c => c.Kind == ChangeKind.Added), Is.True);

            var paged = _repo.GetLogAsync(1, 1, None).GetAwaiter().GetResult();
            Assert.That(paged.Select(s => s.Hash), Is.EqualTo(new[] { first }));

            var files = _repo.GetChangedFilesAsync(second, None).GetAwaiter().GetResult();
            Assert.That(files.Select(f => f.Path), Is.EquivalentTo(new[] { "Assets/a b.txt", "Assets/日本語.txt.meta", "Assets/new.txt" }));
        }

        [Test]
        public void Diff_TrackedAndUntracked()
        {
            InitWithIdentity();
            _dir.WriteText("Assets/t.txt", "line1\n");
            Save("first");
            _dir.WriteText("Assets/t.txt", "line1\nline2\n");
            _dir.WriteText("Assets/u.txt", "brand new\n");

            var tracked = _repo.GetDiffAsync("Assets/t.txt", false, None).GetAwaiter().GetResult();
            Assert.That(tracked, Does.Contain("+line2"));
            Assert.That(tracked, Does.Not.Contain("+line1"));

            var untracked = _repo.GetDiffAsync("Assets/u.txt", true, None).GetAwaiter().GetResult();
            Assert.That(untracked, Does.Contain("+brand new"));

            var unchanged = _repo.GetDiffAsync("Assets/missing.txt", false, None).GetAwaiter().GetResult();
            Assert.That(unchanged, Is.Empty);
        }

        [Test]
        public void ReadTree_RestoresTrackedFilesWithoutMovingHead()
        {
            InitWithIdentity();
            _dir.WriteText("Assets/a.txt", "v1");
            _dir.WriteText("Assets/gone.txt", "to be deleted");
            var first = Save("first");
            _dir.WriteText("Assets/a.txt", "v2");
            System.IO.File.Delete(_dir.File("Assets/gone.txt"));
            _dir.WriteText("Assets/b.txt", "added later");
            var second = Save("second");

            _repo.ReadTreeAsync(first, None).GetAwaiter().GetResult();

            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("v1"));
            Assert.That(_dir.ReadText("Assets/gone.txt"), Is.EqualTo("to be deleted"));
            Assert.That(System.IO.File.Exists(_dir.File("Assets/b.txt")), Is.False);
            Assert.That(_repo.GetHeadAsync(None).GetAwaiter().GetResult(), Is.EqualTo(second), "read-tree must not move HEAD");

            var status = _repo.GetStatusAsync(None).GetAwaiter().GetResult();
            Assert.That(status.HasChanges, Is.True);

            var restore = Save("Restore: first");
            var log = _repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult();
            Assert.That(log.Select(s => s.Hash), Is.EqualTo(new[] { restore, second, first }), "history stays linear");
            Assert.That(_repo.GetStatusAsync(None).GetAwaiter().GetResult().HasChanges, Is.False);
        }

        [Test]
        public void Stash_PushAndList()
        {
            InitWithIdentity();
            _dir.WriteText("Assets/a.txt", "v1");
            Save("first");

            Assert.That(_repo.StashPushAsync("shiori:auto-before-restore", true, None).GetAwaiter().GetResult(), Is.Null, "nothing to stash");

            _dir.WriteText("Assets/a.txt", "dirty");
            _dir.WriteText("Assets/untracked.txt", "u");
            var stashHash = _repo.StashPushAsync("shiori:auto-before-restore", true, None).GetAwaiter().GetResult();

            Assert.That(stashHash, Has.Length.EqualTo(40));
            Assert.That(_dir.ReadText("Assets/a.txt"), Is.EqualTo("v1"));
            Assert.That(System.IO.File.Exists(_dir.File("Assets/untracked.txt")), Is.False);
            Assert.That(_repo.GetStatusAsync(None).GetAwaiter().GetResult().HasChanges, Is.False);

            var list = _repo.StashListAsync(None).GetAwaiter().GetResult();
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].Selector, Is.EqualTo("stash@{0}"));
            Assert.That(list[0].Hash, Is.EqualTo(stashHash));
            Assert.That(list[0].Message, Does.Contain("shiori:auto-before-restore"));
        }

        [Test]
        public void Commit_WithoutIdentityFailsWithGitException()
        {
            _repo.InitAsync(None).GetAwaiter().GetResult();
            // Force an empty identity regardless of the machine's global config.
            _repo.SetLocalConfigAsync("user.useConfigOnly", "true", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("user.name", "", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("user.email", "", None).GetAwaiter().GetResult();
            _dir.WriteText("a.txt", "a");
            _repo.AddAllAsync(None).GetAwaiter().GetResult();

            var ex = Assert.Throws<GitException>(() => _repo.CommitAsync("x", None).GetAwaiter().GetResult());
            Assert.That(ex.ExitCode, Is.Not.EqualTo(0));
            Assert.That(ex.Stderr, Is.Not.Empty);
        }

        [Test]
        public void Cancellation_IsObserved()
        {
            InitWithIdentity();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.Catch<System.OperationCanceledException>(() => _repo.GetStatusAsync(cts.Token).GetAwaiter().GetResult());
            }
        }
    }
}
