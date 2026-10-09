using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Shiori.Editor.Tests
{
    /// <summary>
    /// The wizard's first save must leave a clean working tree: ProjectSettings/Shiori.json has to be
    /// part of the initial snapshot, not an uncommitted change after it.
    /// ShioriSession resumes on the main thread, so the test yields instead of blocking.
    /// </summary>
    public class FirstSaveTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-firstsave-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
            File.WriteAllText(Path.Combine(_root, "Assets", "a.txt"), "a");
        }

        [TearDown]
        public void TearDown()
        {
            if (!Directory.Exists(_root)) return;
            foreach (var info in new DirectoryInfo(_root).GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if ((info.Attributes & FileAttributes.ReadOnly) != 0) info.Attributes &= ~FileAttributes.ReadOnly;
            }
            Directory.Delete(_root, true);
        }

        private static IEnumerator Await(Task task)
        {
            var started = System.DateTime.UtcNow;
            while (!task.IsCompleted)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("task did not finish within 60 seconds");
                yield return null;
            }
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion), task.Exception?.ToString());
        }

        [UnityTest]
        public IEnumerator FirstSave_CommitsTheSettingsFileAndLeavesNothingUncommitted()
        {
            var session = new ShioriSession(_root);
            var locate = session.LocateGitAsync(CancellationToken.None);
            yield return Await(locate);
            Assume.That(locate.Result.Found && locate.Result.MeetsMinimumVersion, Is.True, "a supported git is required");

            session.WriteIgnoreFiles();
            yield return Await(session.FirstSaveAsync("Shiori Test", "shiori@example.com", CancellationToken.None));

            Assert.That(session.Project.SetupCompleted, Is.True);
            Assert.That(new ShioriSession(_root).Project.SetupCompleted, Is.True, "the flag was written to disk");

            var status = session.Repository.GetStatusAsync(CancellationToken.None);
            yield return Await(status);
            Assert.That(status.Result.HasChanges, Is.False, "nothing may be left uncommitted after the first save");

            var log = session.Repository.GetLogAsync(10, 0, CancellationToken.None);
            yield return Await(log);
            Assert.That(log.Result.Count, Is.EqualTo(1));
            Assert.That(log.Result[0].Message, Is.EqualTo(ShioriSession.InitialCommitMessage));
            Assert.That(log.Result[0].Changes.Select(c => c.Path), Does.Contain("ProjectSettings/Shiori.json"));
            Assert.That(log.Result[0].Changes.Select(c => c.Path), Does.Contain(".gitignore"));
        }

        [UnityTest]
        public IEnumerator FirstSave_OnAnExistingCleanRepository_DoesNotFail()
        {
            var session = new ShioriSession(_root);
            yield return Await(session.LocateGitAsync(CancellationToken.None));
            Assume.That(session.Repository, Is.Not.Null, "a supported git is required");

            yield return Await(session.FirstSaveAsync("Shiori Test", "shiori@example.com", CancellationToken.None));
            yield return Await(session.FirstSaveAsync(null, null, CancellationToken.None));

            var log = session.Repository.GetLogAsync(10, 0, CancellationToken.None);
            yield return Await(log);
            Assert.That(log.Result.Count, Is.EqualTo(1), "a second run with nothing new to commit adds no commit");
        }
    }
}
