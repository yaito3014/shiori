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
    /// The one end-to-end restore test the spec asks for: a real git repository in a temporary
    /// directory, driven through RestoreOperation (AssetDatabase calls included).
    /// RestoreOperation resumes on the editor main thread, so the test must yield instead of
    /// blocking on the task; blocking would deadlock the editor.
    /// </summary>
    public class RestoreOperationTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private string _gitPath;
        private string _root;
        private GitRepository _repo;

        [SetUp]
        public void SetUp()
        {
            var location = new GitLocator(new ProcessGitRunner()).LocateAsync(null, None).GetAwaiter().GetResult();
            Assume.That(location.Found && location.MeetsMinimumVersion, Is.True, "a supported git is required");
            _gitPath = location.Path;

            _root = Path.Combine(Path.GetTempPath(), "shiori-restore-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _repo = new GitRepository(new ProcessGitRunner(), _gitPath, _root);
            _repo.InitAsync(None).GetAwaiter().GetResult();
            _repo.SetIdentityAsync("Shiori Test", "shiori@example.com", None).GetAwaiter().GetResult();
            _repo.SetLocalConfigAsync("core.autocrlf", "false", None).GetAwaiter().GetResult();
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

        private void Write(string relative, string content)
        {
            var full = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
        }

        private string Save(string message)
        {
            _repo.AddAllAsync(None).GetAwaiter().GetResult();
            return _repo.CommitAsync(message, None).GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator RestoreTwoStepsBack_WithoutSaving_RestoresTreeAndKeepsStash()
        {
            Write("Assets/Scenes/Main.unity", "scene v1");
            Write("Assets/Scenes/Main.unity.meta", "meta");
            var first = Save("first");
            Write("Assets/Scenes/Main.unity", "scene v2");
            Save("second");
            Write("Assets/Scenes/Main.unity", "scene v3");
            Save("third");
            Write("Assets/Scenes/Main.unity", "unsaved v4");

            var task = RestoreOperation.RunAsync(_repo, first, "first", RestoreMode.StashFirst, null, None);
            var started = System.DateTime.UtcNow;
            while (!task.IsCompleted)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("restore did not finish within 60 seconds");
                yield return null;
            }
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion), task.Exception?.ToString());
            var result = task.Result;

            Assert.That(File.ReadAllText(Path.Combine(_root, "Assets/Scenes/Main.unity")), Is.EqualTo("scene v1"));
            Assert.That(result.ChangedAnything, Is.True);
            Assert.That(result.ChangedScenePaths, Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
            Assert.That(result.TouchedProjectSettings, Is.False);

            var diff = new ProcessGitRunner().RunAsync(_gitPath, new[] { "diff", "--quiet", first }, _root, None).GetAwaiter().GetResult();
            Assert.That(diff.ExitCode, Is.EqualTo(0), "working tree matches the restored snapshot");

            var log = _repo.GetLogAsync(10, 0, None).GetAwaiter().GetResult();
            Assert.That(log.Count, Is.EqualTo(4), "exactly one Restore commit was added");
            Assert.That(log[0].Message, Is.EqualTo("Restore: first"));

            var stashes = _repo.StashListAsync(None).GetAwaiter().GetResult();
            Assert.That(stashes.Select(s => s.Message).Single(), Does.Contain("shiori:auto-before-restore"));
        }

        [Test]
        public void ReloadOpenScenes_IgnoresScenesThatAreNotLoaded()
        {
            // The dev project has no scene at this path, so nothing is reopened and nothing throws.
            Assert.DoesNotThrow(() => RestoreOperation.ReloadOpenScenes(new[] { "Assets/NotLoaded.unity" }));
            Assert.DoesNotThrow(() => RestoreOperation.ReloadOpenScenes(null));
        }
    }
}
