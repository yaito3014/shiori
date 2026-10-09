using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>受信 between two working copies sharing a bare repository on disk.</summary>
    public class ReceiveRunnerTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _here;
        private TempDirectory _there;
        private TempDirectory _remoteDir;
        private GitRepository _mine;
        private GitRepository _other;
        private string _remoteUrl;

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
            _mine.SetRemoteUrlAsync(_remoteUrl, None).GetAwaiter().GetResult();
            SendRunner.SendAsync(_mine, None).GetAwaiter().GetResult();

            // The other PC starts from what this PC sent.
            Run(_there.Path, "clone", "-q", _remoteUrl, _there.Path);
            _other = new GitRepository(new ProcessGitRunner(), _gitPath, _there.Path);
            _other.SetIdentityAsync("Other PC", "other@example.com", None).GetAwaiter().GetResult();
            _other.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
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

        private void OtherSends(string file, string text)
        {
            Save(_other, _there, file, text);
            Assert.That(SendRunner.SendAsync(_other, None).GetAwaiter().GetResult().Outcome, Is.EqualTo(SendOutcome.Sent));
        }

        private ReceiveResult Receive() => ReceiveRunner.ReceiveAsync(_mine, None).GetAwaiter().GetResult();

        [Test]
        public void NothingNew_IsReported()
        {
            Assert.That(Receive().Outcome, Is.EqualTo(ReceiveOutcome.NothingToReceive));
        }

        [Test]
        public void NewSavesFromAnotherPc_AreReceivedByFastForward()
        {
            OtherSends("Assets/a.txt", "v2 from the other PC");
            OtherSends("Assets/b.txt", "new from the other PC");

            var check = ReceiveRunner.CheckAsync(_mine, None).GetAwaiter().GetResult();
            Assert.That(check.Known, Is.True);
            Assert.That(check.Behind, Is.EqualTo(2), "the background check sees what is waiting");
            Assert.That(_here.ReadText("Assets/a.txt"), Is.EqualTo("v1"), "checking never changes files");

            var result = Receive();

            Assert.That(result.Outcome, Is.EqualTo(ReceiveOutcome.Received));
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.ChangedPaths, Is.EquivalentTo(new[] { "Assets/a.txt", "Assets/b.txt" }));
            Assert.That(_here.ReadText("Assets/a.txt"), Is.EqualTo("v2 from the other PC"));
            Assert.That(_here.ReadText("Assets/b.txt"), Is.EqualTo("new from the other PC"));
            Assert.That(ReceiveRunner.CompareAsync(_mine, None).GetAwaiter().GetResult().Behind, Is.EqualTo(0));
        }

        [Test]
        public void UnsavedChanges_BlockAndNothingIsFetchedIntoTheTree()
        {
            OtherSends("Assets/a.txt", "v2");
            _here.WriteText("Assets/a.txt", "unsaved here");

            Assert.That(Receive().Outcome, Is.EqualTo(ReceiveOutcome.UnsavedChanges));
            Assert.That(_here.ReadText("Assets/a.txt"), Is.EqualTo("unsaved here"));
        }

        [Test]
        public void SplitHistories_AreReportedAndLeftAlone()
        {
            OtherSends("Assets/other.txt", "from there");
            var mine = Save(_mine, _here, "Assets/mine.txt", "from here");

            var result = Receive();

            Assert.That(result.Outcome, Is.EqualTo(ReceiveOutcome.Diverged));
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(_mine.GetHeadAsync(None).GetAwaiter().GetResult(), Is.EqualTo(mine), "no merge, no rebase");
            Assert.That(System.IO.File.Exists(_here.File("Assets/other.txt")), Is.False);
            var compare = ReceiveRunner.CompareAsync(_mine, None).GetAwaiter().GetResult();
            Assert.That(compare.Diverged, Is.True);
        }

        [Test]
        public void AnotherProjectsHistory_IsReportedAsUnrelated()
        {
            using (var strangerDir = new TempDirectory("shiori-stranger"))
            {
                var stranger = Init(strangerDir);
                Save(stranger, strangerDir, "Assets/x.txt", "another project");
                stranger.SetRemoteUrlAsync(_remoteUrl, None).GetAwaiter().GetResult();

                Assert.That(ReceiveRunner.ReceiveAsync(stranger, None).GetAwaiter().GetResult().Outcome, Is.EqualTo(ReceiveOutcome.Unrelated));
                Assert.That(ReceiveRunner.CompareAsync(stranger, None).GetAwaiter().GetResult().Known, Is.False);
            }
        }

        [Test]
        public void NoRemote_IsReported()
        {
            using (var loneDir = new TempDirectory("shiori-lone"))
            {
                var lone = Init(loneDir);
                Save(lone, loneDir, "Assets/a.txt", "v1");
                Assert.That(ReceiveRunner.ReceiveAsync(lone, None).GetAwaiter().GetResult().Outcome, Is.EqualTo(ReceiveOutcome.NoRemote));
                Assert.That(ReceiveRunner.CheckAsync(lone, None).GetAwaiter().GetResult().Known, Is.False, "no network is touched without a 送信先");
            }
        }

        [Test]
        public void BackgroundFetch_PassesTheNoPromptSettings()
        {
            var runner = new FakeGitRunner();
            var repo = new GitRepository(runner, "git", "C:/p");
            repo.FetchAsync(false, None).GetAwaiter().GetResult();
            repo.FetchAsync(true, None).GetAwaiter().GetResult();
            Assert.That(runner.Calls[0].Joined, Is.EqualTo("-c credential.interactive=false -c core.sshCommand=ssh -o BatchMode=yes fetch --prune --no-tags origin"));
            Assert.That(runner.Calls[1].Joined, Is.EqualTo("fetch --prune --no-tags origin"), "a 受信 the user started may show the sign-in window");
        }
    }
}
