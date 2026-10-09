using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Shiori.Editor.Tests
{
    /// <summary>The two extension points added for A2: the 戻す preview with its async warning, and state-derived notices.</summary>
    public class RestorePreviewAndNoticeTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-preview-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
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

        /// <summary>Only the old synchronous hook: the default async one must fall back to it.</summary>
        private sealed class SyncOnlyExtension : ShioriExtension
        {
            public override string PackageId => "com.example.synconly";
            public override string GetRestoreWarning(IExtensionContext context, Snapshot target) => "sync warning for " + target.Message;
        }

        private sealed class ThrowingExtension : ShioriExtension
        {
            public override string PackageId => "com.example.throws";
            public override Task<string> GetRestoreWarningAsync(IExtensionContext context, RestorePreview preview, CancellationToken cancellationToken) => throw new System.InvalidOperationException("boom");
            public override Task<IReadOnlyList<ExtensionNotice>> GetNoticesAsync(IExtensionContext context, CancellationToken cancellationToken) => throw new System.InvalidOperationException("boom");
        }

        [UnityTest]
        public IEnumerator Preview_ListsWhatTheRestoreChanges_AndWarningsSeeIt()
        {
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { new FakeExtension(), new SyncOnlyExtension(), new ThrowingExtension() });
            yield return Await(session.LocateGitAsync(CancellationToken.None));
            Assume.That(session.Repository, Is.Not.Null, "a supported git is required");

            File.WriteAllText(Path.Combine(_root, "Assets", "a.txt"), "v1");
            File.WriteAllText(Path.Combine(_root, "Assets", "same.txt"), "same");
            yield return Await(session.FirstSaveAsync("Shiori Test", "shiori@example.com", CancellationToken.None));
            var log = session.Repository.GetLogAsync(1, 0, CancellationToken.None);
            yield return Await(log);
            var first = log.Result[0];

            File.WriteAllText(Path.Combine(_root, "Assets", "a.txt"), "v2");
            File.WriteAllText(Path.Combine(_root, "Assets", "b.txt"), "new");
            yield return Await(session.Repository.AddAllAsync(CancellationToken.None));
            yield return Await(session.Repository.CommitAsync("second", CancellationToken.None));

            var preview = session.PreviewRestoreAsync(first, CancellationToken.None);
            yield return Await(preview);
            Assert.That(preview.Result.Target, Is.SameAs(first));
            Assert.That(preview.Result.ChangedPaths, Is.EqualTo(new[] { "Assets/a.txt", "Assets/b.txt" }));
            Assert.That(preview.Result.Changes("Assets/a.txt"), Is.True);
            Assert.That(preview.Result.Changes("Assets\\b.txt"), Is.True, "backslashes are accepted");
            Assert.That(preview.Result.Changes("Assets/same.txt"), Is.False);

            ExpectThrowingExtensionLogged();
            var warning = session.GetRestoreWarningAsync(preview.Result, CancellationToken.None);
            yield return Await(warning);
            Assert.That(warning.Result, Is.EqualTo(FakeExtension.RestoreWarning + " (2)\n\nsync warning for " + first.Message),
                "async warnings first in package order, the sync-only one through the default, the throwing one skipped");
        }

        /// <summary>A failing extension is logged once per call, naming its package and the cause.</summary>
        private static void ExpectThrowingExtensionLogged()
        {
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"com\.example\.throws failed: boom"));
        }

        [UnityTest]
        public IEnumerator Notices_FollowTheProjectState()
        {
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { new FakeExtension(), new ThrowingExtension() });

            ExpectThrowingExtensionLogged();
            var none = session.GetNoticesAsync(CancellationToken.None);
            yield return Await(none);
            Assert.That(none.Result, Is.Empty);

            File.WriteAllText(Path.Combine(_root, FakeExtension.NoticeFile), "x");
            ExpectThrowingExtensionLogged();
            var one = session.GetNoticesAsync(CancellationToken.None);
            yield return Await(one);
            Assert.That(one.Result.Count, Is.EqualTo(1));
            var notice = one.Result[0];
            Assert.That(notice.Step.Id, Is.EqualTo(FakeExtension.NoticeId));
            Assert.That(notice.Title, Is.EqualTo(FakeExtension.NoticeTitle));
            Assert.That(notice.Done, Is.False, "a notice always shows until the extension stops returning it");
            Assert.That(notice.View.Message, Is.EqualTo("notice message"));
            Assert.That(notice.View.Detail, Is.EqualTo("notice detail"));

            yield return Await(notice.View.Actions[0].Run(CancellationToken.None));
            ExpectThrowingExtensionLogged();
            var after = session.GetNoticesAsync(CancellationToken.None);
            yield return Await(after);
            Assert.That(after.Result, Is.Empty, "fixing the cause removes the notice");
        }

        [Test]
        public void ExtensionNotice_RequiresAnId()
        {
            Assert.That(() => new ExtensionNotice("", "t", "m"), Throws.ArgumentException);
            var notice = new ExtensionNotice("id", null, null);
            Assert.That(notice.Title, Is.Empty);
            Assert.That(notice.Actions, Is.Empty);
        }
    }
}
