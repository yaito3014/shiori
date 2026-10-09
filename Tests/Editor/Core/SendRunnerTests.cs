using System;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>送信 against a bare repository on disk, so no network or sign-in is involved.</summary>
    public class SendRunnerTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private static string _gitPath;
        private TempDirectory _dir;
        private TempDirectory _remoteDir;
        private TempDirectory _otherDir;
        private GitRepository _repo;
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
            _dir = new TempDirectory();
            _remoteDir = new TempDirectory("shiori-remote");
            _otherDir = new TempDirectory("shiori-other");
            _repo = Init(_dir);
            var bare = new ProcessGitRunner().RunAsync(_gitPath, new[] { "init", "--bare", "-q", _remoteDir.Path }, _remoteDir.Path, None).GetAwaiter().GetResult();
            Assert.That(bare.Succeeded, Is.True, bare.Stderr);
            _remoteUrl = _remoteDir.Path.Replace('\\', '/');
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
            _remoteDir.Dispose();
            _otherDir.Dispose();
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

        private SendResult Send() => SendRunner.SendAsync(_repo, None).GetAwaiter().GetResult();
        private SendStatus Status() => SendRunner.GetStatusAsync(_repo, None).GetAwaiter().GetResult();
        private RemoteCheckState Check(GitRepository repo, string url) => RemoteChecker.CheckAsync(repo, url, None).GetAwaiter().GetResult();

        [Test]
        public void NoRemote_IsReportedWithoutTouchingTheNetwork()
        {
            Save(_repo, _dir, "Assets/a.txt", "v1");
            Assert.That(Status().HasRemote, Is.False);
            Assert.That(Send().Outcome, Is.EqualTo(SendOutcome.NoRemote));
        }

        [Test]
        public void FirstSend_PushesEverything_ThenCountsOnlyNewSaves()
        {
            Save(_repo, _dir, "Assets/a.txt", "v1");
            Save(_repo, _dir, "Assets/a.txt", "v2");
            _repo.SetRemoteUrlAsync(_remoteUrl, None).GetAwaiter().GetResult();
            Assert.That(_repo.GetRemoteUrlAsync(None).GetAwaiter().GetResult(), Is.EqualTo(_remoteUrl));

            var before = Status();
            Assert.That(before.NeverSent, Is.True);
            Assert.That(before.Unsent, Is.EqualTo(2));

            var sent = Send();
            Assert.That(sent.Outcome, Is.EqualTo(SendOutcome.Sent));
            Assert.That(sent.Count, Is.EqualTo(2));
            Assert.That(Status().Unsent, Is.EqualTo(0));
            Assert.That(Status().NeverSent, Is.False);
            Assert.That(Send().Outcome, Is.EqualTo(SendOutcome.NothingToSend));

            Save(_repo, _dir, "Assets/b.txt", "new");
            _dir.WriteText("Assets/unsaved.txt", "not saved");
            Assert.That(Status().Unsent, Is.EqualTo(1));
            var again = Send();
            Assert.That(again.Outcome, Is.EqualTo(SendOutcome.Sent));
            Assert.That(again.Count, Is.EqualTo(1));
            Assert.That(again.HadUnsavedChanges, Is.True, "unsaved work is not sent, and the UI says so");
        }

        [Test]
        public void SendAfterAnotherPcSent_IsRejectedAndChangesNothing()
        {
            Save(_repo, _dir, "Assets/a.txt", "v1");
            _repo.SetRemoteUrlAsync(_remoteUrl, None).GetAwaiter().GetResult();
            Send();

            // Another PC clones, saves and sends first.
            var clone = new ProcessGitRunner().RunAsync(_gitPath, new[] { "clone", "-q", _remoteUrl, _otherDir.Path }, _otherDir.Path, None).GetAwaiter().GetResult();
            Assert.That(clone.Succeeded, Is.True, clone.Stderr);
            var other = new GitRepository(new ProcessGitRunner(), _gitPath, _otherDir.Path);
            other.SetIdentityAsync("Other PC", "other@example.com", None).GetAwaiter().GetResult();
            Save(other, _otherDir, "Assets/other.txt", "from the other PC");
            Assert.That(SendRunner.SendAsync(other, None).GetAwaiter().GetResult().Outcome, Is.EqualTo(SendOutcome.Sent));

            var mine = Save(_repo, _dir, "Assets/mine.txt", "from this PC");
            var ex = Assert.Throws<RemoteOperationException>(() => Send());
            Assert.That(ex.Kind, Is.EqualTo(RemoteErrorKind.Rejected), ex.Stderr);
            Assert.That(_repo.GetHeadAsync(None).GetAwaiter().GetResult(), Is.EqualTo(mine), "nothing was merged or rewritten");
        }

        [Test]
        public void Check_TellsEmptySameAndOtherHistoryApart()
        {
            Save(_repo, _dir, "Assets/a.txt", "v1");
            Assert.That(Check(_repo, _remoteUrl), Is.EqualTo(RemoteCheckState.Empty));

            _repo.SetRemoteUrlAsync(_remoteUrl, None).GetAwaiter().GetResult();
            Send();
            Assert.That(Check(_repo, _remoteUrl), Is.EqualTo(RemoteCheckState.SameHistory));

            var stranger = Init(_otherDir);
            Save(stranger, _otherDir, "Assets/x.txt", "another project");
            Assert.That(Check(stranger, _remoteUrl), Is.EqualTo(RemoteCheckState.OtherHistory));
        }

        [Test]
        public void Check_OfAMissingRepository_IsClassified()
        {
            Save(_repo, _dir, "Assets/a.txt", "v1");
            var missing = _remoteUrl + "-does-not-exist";
            var ex = Assert.Throws<RemoteOperationException>(() => Check(_repo, missing));
            Assert.That(ex.Kind, Is.EqualTo(RemoteErrorKind.NotFound), ex.Stderr);
        }

        [Test]
        public void SetRemoteUrl_ReplacesAnExistingOne()
        {
            Save(_repo, _dir, "Assets/a.txt", "v1");
            _repo.SetRemoteUrlAsync("https://example.com/a.git", None).GetAwaiter().GetResult();
            _repo.SetRemoteUrlAsync("https://example.com/b.git", None).GetAwaiter().GetResult();
            Assert.That(_repo.GetRemoteUrlAsync(None).GetAwaiter().GetResult(), Is.EqualTo("https://example.com/b.git"));
            Assert.That(() => _repo.SetRemoteUrlAsync(" ", None).GetAwaiter().GetResult(), Throws.ArgumentException);
        }

        [TestCase("remote: error: GH001: Large files detected. You may want to try Git Large File Storage", RemoteErrorKind.TooLarge)]
        [TestCase("remote: error: File Assets/big.fbx is 120.00 MB; this exceeds GitHub's file size limit of 100.00 MB", RemoteErrorKind.TooLarge)]
        [TestCase("!\trefs/heads/main:refs/heads/main\t[rejected] (fetch first)", RemoteErrorKind.Rejected)]
        [TestCase("hint: Updates were rejected because the tip of your current branch is behind", RemoteErrorKind.Rejected)]
        [TestCase("fatal: Authentication failed for 'https://github.com/a/b.git/'", RemoteErrorKind.Authentication)]
        [TestCase("fatal: could not read Username for 'https://github.com': terminal prompts disabled", RemoteErrorKind.Authentication)]
        [TestCase("The requested URL returned error: 403", RemoteErrorKind.Authentication)]
        [TestCase("remote: Repository not found.\nfatal: repository 'https://github.com/a/b.git/' not found", RemoteErrorKind.NotFound)]
        [TestCase("fatal: unable to access 'https://github.com/a/b.git/': Could not resolve host: github.com", RemoteErrorKind.Network)]
        [TestCase("To x\n=\trefs/heads/main:refs/heads/main\ta4031234..a4045678\tsomething odd", RemoteErrorKind.Unknown)]
        public void Classifier_MapsGitOutput(string output, RemoteErrorKind expected)
        {
            Assert.That(RemoteErrorClassifier.Classify(output), Is.EqualTo(expected));
        }

        [Test]
        public void Timeout_IsReportedAsTimeout()
        {
            var saved = GitRepository.ListRemoteTimeout;
            try
            {
                GitRepository.ListRemoteTimeout = TimeSpan.FromMilliseconds(1);
                var ex = Assert.Throws<RemoteOperationException>(() => _repo.ListRemoteHeadsAsync(_remoteUrl, None).GetAwaiter().GetResult());
                Assert.That(ex.Kind, Is.EqualTo(RemoteErrorKind.Timeout));
            }
            finally
            {
                GitRepository.ListRemoteTimeout = saved;
            }
        }
    }
}
