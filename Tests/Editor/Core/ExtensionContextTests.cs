using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class ExtensionContextTests
    {
        private TempDirectory _dir;
        private ShioriProjectSettings _project;
        private int _saves;
        private ExtensionContext _context;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDirectory();
            _project = new ShioriProjectSettings();
            _saves = 0;
            _context = new ExtensionContext(_dir.Path, "ja", () => null, () => _project, () => _saves++, "com.example.ext");
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
        }

        [Test]
        public void Settings_AreTheExtensionSectionAndSaveCallsBack()
        {
            Assert.That(_context.Settings, Is.Empty);
            _context.Settings["k"] = "v";
            _context.SaveSettings();
            Assert.That(_saves, Is.EqualTo(1));
            Assert.That(_project.Extensions["com.example.ext"]["k"], Is.EqualTo("v"));
            Assert.That(_project.GetExtensionSettings("com.example.ext"), Is.SameAs(_context.Settings));
        }

        [Test]
        public void Settings_FollowTheCurrentProjectObject()
        {
            _context.Settings["k"] = "old";
            _project = new ShioriProjectSettings();
            Assert.That(_context.Settings, Is.Empty, "a reloaded settings object is picked up");
        }

        [Test]
        public void ManagedBlocks_AreLimitedToTheTwoFiles()
        {
            Assert.That(_context.ReadManagedBlock(".gitignore", "x"), Is.Null);
            Assert.That(_context.UpsertManagedBlock(".gitignore", "x", new[] { "a/" }), Is.True);
            Assert.That(_context.UpsertManagedBlock(".gitignore", "x", new[] { "a/" }), Is.False);
            Assert.That(_context.ReadManagedBlock(".gitignore", "x"), Is.EqualTo(new[] { "a/" }));
            Assert.That(File.Exists(_dir.File(".gitignore")), Is.True);

            Assert.That(_context.UpsertManagedBlock(".gitattributes", "x", new[] { "*.fbx filter=lfs" }), Is.True);
            Assert.That(() => _context.UpsertManagedBlock("gitignore", "x", new string[0]), Throws.ArgumentException);
            Assert.That(() => _context.UpsertManagedBlock(".GitIgnore", "x", new string[0]), Throws.ArgumentException);
            Assert.That(() => _context.UpsertManagedBlock("sub/.gitignore", "x", new string[0]), Throws.ArgumentException);
            Assert.That(() => _context.ReadManagedBlock(Path.Combine(_dir.Path, ".gitignore"), "x"), Throws.ArgumentException);
        }

        [Test]
        public void Constructor_RequiresEverything()
        {
            Assert.That(() => new ExtensionContext("", "ja", () => null, () => _project, () => { }, "p"), Throws.ArgumentException);
            Assert.That(() => new ExtensionContext(_dir.Path, "ja", () => null, () => _project, () => { }, ""), Throws.ArgumentException);
            Assert.That(() => new ExtensionContext(_dir.Path, "ja", null, () => _project, () => { }, "p"), Throws.ArgumentNullException);
            Assert.That(new ExtensionContext(_dir.Path, null, () => null, () => _project, () => { }, "p").LanguageCode, Is.EqualTo("en"));
        }

        [Test]
        public void SettingsStore_RoundTripsExtensionSectionsAndKeepsUnknownOnes()
        {
            var store = new SettingsStore(_dir.Path);
            var settings = new ShioriProjectSettings { SetupCompleted = true };
            settings.GetExtensionSettings("com.a")["flag"] = true;
            settings.GetExtensionSettings("com.b")["list"] = new List<object> { "x", 2L };
            settings.GetExtensionSettings("com.empty");
            store.SaveProject(settings);

            var text = _dir.ReadText("ProjectSettings/Shiori.json");
            Assert.That(text, Does.Contain("\"extensions\""));
            Assert.That(text, Does.Not.Contain("com.empty"), "empty sections are not written");

            var loaded = store.LoadProject();
            Assert.That(loaded.SetupCompleted, Is.True);
            Assert.That(loaded.Extensions.Keys, Is.EquivalentTo(new[] { "com.a", "com.b" }));
            Assert.That(loaded.Extensions["com.a"]["flag"], Is.True);
            Assert.That(loaded.Extensions["com.b"]["list"], Is.EqualTo(new List<object> { "x", 2L }));

            // A section of a package that is not installed survives a save by the core.
            loaded.SetupCompleted = false;
            store.SaveProject(loaded);
            Assert.That(store.LoadProject().Extensions["com.b"]["list"], Is.EqualTo(new List<object> { "x", 2L }));
        }

        [Test]
        public void SettingsStore_WithoutExtensions_KeepsTheM1FileShape()
        {
            var store = new SettingsStore(_dir.Path);
            store.SaveProject(new ShioriProjectSettings { SetupCompleted = true });
            Assert.That(_dir.ReadText("ProjectSettings/Shiori.json"), Is.EqualTo("{\n  \"schemaVersion\": 1,\n  \"setupCompleted\": true\n}\n"));
        }
    }
}
