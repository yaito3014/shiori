using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Shiori.Editor.Tests
{
    /// <summary>「大きなファイルの設定」 as the wizard and the notices see it, and the guard that stops taking files out.</summary>
    public class LargeFileStepTests
    {
        private static readonly CancellationToken None = CancellationToken.None;

        private string _root;
        private ShioriSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-lfs-step-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
            Directory.CreateDirectory(Path.Combine(_root, "ProjectSettings"));
            _session = new ShioriSession(_root);
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

        private IEnumerator PrepareRepository()
        {
            yield return Await(_session.LocateGitAsync(None));
            Assume.That(_session.Repository, Is.Not.Null, "a supported git is required");
            var hasLfs = _session.Repository.HasLfsAsync(None);
            yield return Await(hasLfs);
            Assume.That(hasLfs.Result, Is.True, "git-lfs is required");
            yield return Await(_session.Repository.InitAsync(None));
        }

        private LargeFileSetupStep Step()
        {
            var core = new CoreSetupSteps();
            return core.CreateSetupSteps(_session.ContextFor(core)).OfType<LargeFileSetupStep>().Single();
        }

        [UnityTest]
        public IEnumerator Skip_IsRecordedForTheProject_AndCanBeUndone()
        {
            yield return PrepareRepository();
            var first = Step().EvaluateAsync(None);
            yield return Await(first);
            Assert.That(first.Result.Done, Is.False);
            Assert.That(first.Result.Actions.Select(a => a.Label), Is.EqualTo(new[] { L10n.Tr("step.lfs.apply"), L10n.Tr("step.lfs.skip") }));

            yield return Await(first.Result.Actions[1].Run(None));
            Assert.That(File.ReadAllText(Path.Combine(_root, "ProjectSettings", "Shiori.json")), Does.Contain("largeFiles"), "every PC sees the choice");
            _session.ReloadSettings();
            var skipped = Step().EvaluateAsync(None);
            yield return Await(skipped);
            Assert.That(skipped.Result.Done, Is.True);
            Assert.That(skipped.Result.Message, Is.EqualTo(L10n.Tr("step.lfs.skipped")));

            yield return Await(skipped.Result.Actions.Single().Run(None));
            var used = Step().EvaluateAsync(None);
            yield return Await(used);
            Assert.That(used.Result.Message, Is.EqualTo(L10n.Tr("step.lfs.ok")));
            Assert.That(File.ReadAllText(Path.Combine(_root, "ProjectSettings", "Shiori.json")), Does.Not.Contain("largeFiles"));
        }

        [UnityTest]
        public IEnumerator Guard_BlocksOnlyWhileThisPcCannotRunLfs()
        {
            yield return PrepareRepository();
            var before = LargeFileGuard.IsBlockedAsync(_session, None);
            yield return Await(before);
            Assert.That(before.Result, Is.False, "a project without LFS is never blocked");

            yield return Await(LargeFileSetup.ApplyAsync(_session.Repository, None));
            var hook = _session.Repository.GetGitPathAsync("hooks/pre-push", None);
            yield return Await(hook);
            File.Delete(hook.Result);

            var blocked = LargeFileGuard.IsBlockedAsync(_session, None);
            yield return Await(blocked);
            Assert.That(blocked.Result, Is.True);
            var view = Step().EvaluateAsync(None);
            yield return Await(view);
            Assert.That(view.Result.Actions.Single().Label, Is.EqualTo(L10n.Tr("step.lfs.install.button")));
        }
    }
}
