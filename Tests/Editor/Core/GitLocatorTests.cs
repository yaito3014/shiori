using System;
using System.Threading;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class GitLocatorTests
    {
        [TestCase("git version 2.47.1.windows.1", "2.47.1")]
        [TestCase("git version 2.30.0", "2.30.0")]
        [TestCase("git version 2.39", "2.39.0")]
        [TestCase("git version 2.45.2 (Apple Git-154)\n", "2.45.2")]
        public void ParseVersion_ReadsMajorMinorPatch(string line, string expected)
        {
            Assert.That(GitLocator.ParseVersion(line), Is.EqualTo(new Version(expected)));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("not git")]
        [TestCase("git: command not found")]
        public void ParseVersion_ReturnsNullWhenUnrecognised(string line)
        {
            Assert.That(GitLocator.ParseVersion(line), Is.Null);
        }

        [Test]
        public void EnumerateCandidates_UsesConfiguredPathThenPathThenDefaults()
        {
            var candidates = GitLocator.EnumerateCandidates(
                @"D:\tools\git.exe",
                $@"C:\a;C:\b",
                isWindows: true,
                defaultDirectories: new[] { @"C:\Program Files\Git\cmd" });

            Assert.That(candidates, Is.EqualTo(new[]
            {
                @"D:\tools\git.exe",
                @"C:\a\git.exe",
                @"C:\b\git.exe",
                @"C:\Program Files\Git\cmd\git.exe",
            }));
        }

        [Test]
        public void EnumerateCandidates_SkipsEmptyAndDuplicateEntries()
        {
            var candidates = GitLocator.EnumerateCandidates(
                null,
                $@"C:\a;;c:\A;  ;C:\b",
                isWindows: true,
                defaultDirectories: new[] { @"C:\b" });

            Assert.That(candidates, Is.EqualTo(new[] { @"C:\a\git.exe", @"C:\b\git.exe" }));
        }

        [Test]
        public void EnumerateCandidates_UsesPlainGitNameOffWindows()
        {
            var candidates = GitLocator.EnumerateCandidates(null, "/usr/bin:/usr/local/bin", isWindows: false, defaultDirectories: null);
            Assert.That(candidates, Is.EqualTo(new[] { "/usr/bin/git", "/usr/local/bin/git" }));
        }

        [Test]
        public void MinimumVersion_IsTwoThirty()
        {
            Assert.That(GitLocator.MinimumVersion, Is.EqualTo(new Version(2, 30)));
            Assert.That(new GitLocation("x", new Version(2, 29, 9), "", null, null).MeetsMinimumVersion, Is.False);
            Assert.That(new GitLocation("x", new Version(2, 30, 0), "", null, null).MeetsMinimumVersion, Is.True);
        }

        [Test]
        public void LocateAsync_ReturnsNotFoundWhenNoCandidateExists()
        {
            var runner = new FakeGitRunner();
            var location = new GitLocator(runner).LocateAsync(@"Z:\definitely\missing\git.exe", CancellationToken.None).GetAwaiter().GetResult();
            // The configured path is probed first even though it does not exist.
            Assert.That(location.Probed[0], Is.EqualTo(@"Z:\definitely\missing\git.exe"));
            // Whatever the machine has on PATH, a fake runner that answers nothing useful yields "not found".
            Assert.That(location.Found, Is.False);
        }
    }
}
