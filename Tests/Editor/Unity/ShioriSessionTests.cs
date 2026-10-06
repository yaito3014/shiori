using System.IO;
using NUnit.Framework;

namespace Shiori.Editor.Tests
{
    public class ShioriSessionTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "shiori-session-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void IgnoreFiles_AreDetectedOnlyWhenBothBlocksMatch()
        {
            var session = new ShioriSession(_root);
            Assert.That(session.AreIgnoreFilesWritten(), Is.False);

            File.WriteAllText(session.GitIgnorePath, "# user line\nLibrary/\n");
            session.WriteIgnoreFiles();

            Assert.That(session.AreIgnoreFilesWritten(), Is.True);
            Assert.That(File.ReadAllText(session.GitIgnorePath), Does.StartWith("# user line\nLibrary/\n"));
            Assert.That(File.Exists(session.GitAttributesPath), Is.True);

            // A stale block (different content) counts as not written so the wizard offers to update it.
            File.WriteAllText(session.GitAttributesPath, ManagedBlockWriter.Upsert(null, new[] { "*.old text" }));
            Assert.That(session.AreIgnoreFilesWritten(), Is.False);
        }

        [Test]
        public void ProjectSettings_RoundTripThroughSession()
        {
            var session = new ShioriSession(_root);
            Assert.That(session.Project.SetupCompleted, Is.False);
            session.Project.SetupCompleted = true;
            session.SaveProjectSettings();

            var again = new ShioriSession(_root);
            Assert.That(again.Project.SetupCompleted, Is.True);
        }
    }
}
