using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Shiori.Editor.Tests
{
    /// <summary>Every main view must build without throwing; a constructor failure leaves the window empty.</summary>
    public class ViewConstructionTests
    {
        private string _root;
        private ShioriSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-views-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "Assets"));
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
            yield return Await(_session.LocateGitAsync(CancellationToken.None));
            Assume.That(_session.Repository, Is.Not.Null, "a supported git is required");
            File.WriteAllText(Path.Combine(_root, "Assets", "a.txt"), "v1");
            yield return Await(_session.FirstSaveAsync("Shiori Test", "shiori@example.com", CancellationToken.None));
            File.WriteAllText(Path.Combine(_root, "Assets", "a.txt"), "v2\n");
            File.WriteAllText(Path.Combine(_root, "Assets", "new.txt"), "new\n");
        }

        [UnityTest]
        public IEnumerator DetailModeView_BuildsAndLoadsStatusAndDiff()
        {
            yield return PrepareRepository();

            var view = new DetailModeView(_session);
            Assert.That(view.Q<ListView>("files-list"), Is.Not.Null);
            Assert.That(view.Q<TwoPaneSplitView>("split"), Is.Not.Null);

            view.RestoreSelection("Assets/a.txt");
            view.RefreshAll();
            var started = System.DateTime.UtcNow;
            while (view.SelectedPath == null || view.Q<ListView>("diff-lines").itemsSource.Count == 0)
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("status / diff did not load");
                yield return null;
            }

            Assert.That(view.Q<ListView>("files-list").itemsSource.Count, Is.EqualTo(2));
            Assert.That(view.SelectedPath, Is.EqualTo("Assets/a.txt"));
            Assert.That(view.Q<ListView>("diff-lines").itemsSource.Count, Is.GreaterThan(0));
            Assert.That(view.Q<Label>("view-error").ClassListContains("shiori-hidden"), Is.True, view.Q<Label>("view-error").text);
        }

        [Test]
        public void DetailModeView_FindsTheEditorsMonospaceFont()
        {
            Assert.That(DetailModeView.Monospace, Is.Not.Null, "none of these paths loaded: " + string.Join(", ", DetailModeView.MonospaceFontPaths));
        }

        [UnityTest]
        public IEnumerator SimpleModeView_BuildsAndLoadsHistory()
        {
            yield return PrepareRepository();

            var view = new SimpleModeView(_session);
            view.RefreshAll();
            var pending = view.Q<ListView>("save-changes");
            var started = System.DateTime.UtcNow;
            // Both the history and the pending-change list are rendered once status and log have loaded.
            while (view.Q<ListView>("history-list").itemsSource.Count == 0 || pending.ClassListContains("shiori-hidden"))
            {
                if ((System.DateTime.UtcNow - started).TotalSeconds > 60) Assert.Fail("history / pending changes did not load");
                yield return null;
            }
            Assert.That(view.Q<Label>("view-error").ClassListContains("shiori-hidden"), Is.True, view.Q<Label>("view-error").text);

            // The pending changes (a.txt modified, new.txt untracked) are listed under the 保存 button.
            Assert.That(pending.itemsSource.Count, Is.EqualTo(2));
            Assert.That(view.Q<Label>("save-status").text, Does.Contain("2"));
        }

        [UnityTest]
        public IEnumerator SetupWizardView_Builds()
        {
            var session = new ShioriSession(_root, new ProcessGitRunner(), System.Array.Empty<ShioriExtension>());
            var status = session.EvaluateSetupAsync(CancellationToken.None);
            yield return Await(status);
            var view = new SetupWizardView(session, status.Result);
            Assert.That(view.Q<VisualElement>("steps").childCount, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator SetupWizardView_ShowsExtensionStepsBetweenIgnoreFilesAndFirstSave()
        {
            var extension = new FakeExtension();
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { extension });
            var status = session.EvaluateSetupAsync(CancellationToken.None);
            yield return Await(status);
            Assert.That(status.Result.StepCount, Is.EqualTo(5));
            Assert.That(status.Result.FirstSaveStep, Is.EqualTo(5));

            var view = new SetupWizardView(session, status.Result);
            var steps = view.Q<VisualElement>("steps");
            Assert.That(steps.childCount, Is.EqualTo(5));
            Assert.That(steps[3].Q<Label>("step-title").text, Is.EqualTo(FakeExtension.StepTitle));
            Assert.That(steps[3].Q<Label>("step-message").text, Is.EqualTo(FakeExtension.TodoMessage));
            Assert.That(steps[4].Q<Label>("step-title").text, Is.EqualTo(L10n.Tr("step4.title")));
        }

        [UnityTest]
        public IEnumerator SimpleModeView_ShowsExtensionStatusLineAndHint()
        {
            yield return PrepareRepository();
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { new FakeExtension() });
            yield return Await(session.LocateGitAsync(CancellationToken.None));

            var view = new SimpleModeView(session);
            Assert.That(view.Q<Label>("status-line").text, Is.EqualTo(FakeExtension.StatusLine));
            Assert.That(view.Q<Label>("status-line").ClassListContains("shiori-hidden"), Is.False);
            Assert.That(view.Q<Label>("save-hint").text, Is.EqualTo(FakeExtension.SaveHint));

            var noExtensions = new ShioriSession(_root, new ProcessGitRunner(), System.Array.Empty<ShioriExtension>());
            yield return Await(noExtensions.LocateGitAsync(CancellationToken.None));
            var plain = new SimpleModeView(noExtensions);
            Assert.That(plain.Q<Label>("status-line").ClassListContains("shiori-hidden"), Is.True);
            Assert.That(plain.Q<Label>("save-hint").ClassListContains("shiori-hidden"), Is.True);
        }
    }
}
