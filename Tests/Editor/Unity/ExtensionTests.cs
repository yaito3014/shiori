using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Shiori.Editor.Tests
{
    /// <summary>The session's side of the extension API: discovery filter, step evaluation, hooks, text collection.</summary>
    public class ExtensionTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-ext-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
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

        [Test]
        public void Discover_NeverReturnsTypesFromTestAssemblies()
        {
            Assert.That(ExtensionRegistry.IsTestAssembly("Shiori.Editor.Tests"), Is.True);
            Assert.That(ExtensionRegistry.IsTestAssembly("Shiori.VRChat.Editor"), Is.False);
            foreach (var extension in ExtensionRegistry.Discover())
            {
                Assert.That(extension, Is.Not.InstanceOf<FakeExtension>());
                Assert.That(extension.PackageId, Is.Not.Empty);
            }
        }

        [Test]
        public void SessionWithoutExtensions_YieldsNoStepsAndNoText()
        {
            var session = new ShioriSession(_root, new ProcessGitRunner(), System.Array.Empty<ShioriExtension>());
            Assert.That(session.Extensions, Is.Empty);
            Assert.That(session.GetStatusLine(), Is.Null);
            Assert.That(session.GetMemoPlaceholder(), Is.Null);
            Assert.That(session.GetRestoreWarning(null), Is.Null);
        }

        [UnityTest]
        public IEnumerator ExtensionStep_IsEvaluatedAndItsActionWritesItsOwnBlock()
        {
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { new FakeExtension() });
            File.WriteAllText(session.GitIgnorePath, "user\n");
            session.WriteIgnoreFiles();

            var evaluate = session.EvaluateExtensionStepsAsync(CancellationToken.None);
            yield return Await(evaluate);
            var steps = evaluate.Result;
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Done, Is.False);
            Assert.That(steps[0].Title, Is.EqualTo(FakeExtension.StepTitle));
            Assert.That(steps[0].View.Actions.Count, Is.EqualTo(1));

            yield return Await(steps[0].View.Actions[0].Run(CancellationToken.None));
            yield return Await(ShioriSession.EvaluateExtensionStepAsync(steps[0], CancellationToken.None));
            Assert.That(steps[0].Done, Is.True);

            // The extension block sits next to the core block; neither disturbed the other or the user line.
            var content = File.ReadAllText(session.GitIgnorePath);
            Assert.That(content, Does.StartWith("user\n"));
            Assert.That(ManagedBlockWriter.ReadBlock(content), Is.EqualTo(ShioriBlocks.GitIgnore));
            Assert.That(ManagedBlockWriter.ReadBlock(content, FakeExtension.BlockId), Is.EqualTo(FakeExtension.Lines));
            Assert.That(session.AreIgnoreFilesWritten(), Is.True);
        }

        [UnityTest]
        public IEnumerator Hooks_AreCalledInOrderAndTextIsCollected()
        {
            var extension = new FakeExtension();
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { extension });

            yield return Await(session.RunBeforeSaveAsync(CancellationToken.None));
            yield return Await(session.RunAfterSaveAsync("abc", CancellationToken.None));
            yield return Await(session.RunAfterRestoreAsync(new RestoreResult(null, null, null, null), CancellationToken.None));
            Assert.That(extension.BeforeSaveCalls, Is.EqualTo(1));
            Assert.That(extension.AfterSaveCalls, Is.EqualTo(1));
            Assert.That(extension.LastCommitHash, Is.EqualTo("abc"));
            Assert.That(extension.AfterRestoreCalls, Is.EqualTo(1));

            Assert.That(session.GetStatusLine(), Is.EqualTo(FakeExtension.StatusLine));
            Assert.That(session.GetMemoPlaceholder(), Is.EqualTo(FakeExtension.MemoPlaceholder));
            Assert.That(session.GetRestoreWarning(null), Is.EqualTo(FakeExtension.RestoreWarning));
            Assert.That(RestoreFlow.WithWarning("body", FakeExtension.RestoreWarning), Is.EqualTo("body\n\n" + FakeExtension.RestoreWarning));
            Assert.That(RestoreFlow.WithWarning("body", null), Is.EqualTo("body"));
        }

        [Test]
        public void ExtensionSettings_RoundTripThroughProjectSettingsFile()
        {
            var extension = new FakeExtension();
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { extension });
            var context = session.ContextFor(extension);
            Assert.That(context.LanguageCode, Is.EqualTo(L10n.LanguageCode));
            Assert.That(context.ProjectRoot, Is.EqualTo(_root));

            context.Settings["ignoredPackages"] = new System.Collections.Generic.List<object> { "com.vrchat.base" };
            context.Settings["enabled"] = true;
            context.SaveSettings();

            var again = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { extension });
            var loaded = again.ContextFor(extension).Settings;
            Assert.That(loaded["enabled"], Is.True);
            Assert.That(loaded["ignoredPackages"], Is.EqualTo(new System.Collections.Generic.List<object> { "com.vrchat.base" }));
            Assert.That(again.Project.SetupCompleted, Is.False, "core keys are untouched");
        }

        [Test]
        public void ExtensionContext_RefusesFilesOtherThanIgnoreAndAttributes()
        {
            var extension = new FakeExtension();
            var session = new ShioriSession(_root, new ProcessGitRunner(), new ShioriExtension[] { extension });
            var context = session.ContextFor(extension);
            Assert.That(() => context.UpsertManagedBlock("Assets/x.txt", "fake", new string[0]), Throws.ArgumentException);
            Assert.That(() => context.ReadManagedBlock("../.gitignore", "fake"), Throws.ArgumentException);
            Assert.That(context.ReadManagedBlock(".gitattributes", "fake"), Is.Null);
        }
    }
}
