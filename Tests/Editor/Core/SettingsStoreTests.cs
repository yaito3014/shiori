using System.IO;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class SettingsStoreTests
    {
        private TempDirectory _dir;
        private SettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDirectory();
            _store = new SettingsStore(_dir.Path);
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
        }

        [Test]
        public void Paths_AreUnderProjectRoot()
        {
            Assert.That(_store.ProjectSettingsPath, Is.EqualTo(Path.Combine(_dir.Path, "ProjectSettings", "Shiori.json")));
            Assert.That(_store.UserSettingsPath, Is.EqualTo(Path.Combine(_dir.Path, "UserSettings", "Shiori.json")));
        }

        [Test]
        public void MissingFiles_YieldDefaults()
        {
            var project = _store.LoadProject();
            Assert.That(project.SchemaVersion, Is.EqualTo(ShioriProjectSettings.CurrentSchemaVersion));
            Assert.That(project.SetupCompleted, Is.False);

            var user = _store.LoadUser();
            Assert.That(user.SchemaVersion, Is.EqualTo(ShioriUserSettings.CurrentSchemaVersion));
            Assert.That(user.GitPath, Is.Empty);
            Assert.That(user.Mode, Is.EqualTo(UiMode.Simple));
            Assert.That(user.LastTab, Is.Empty);
        }

        [Test]
        public void Project_RoundTripsAndWritesStableJson()
        {
            _store.SaveProject(new ShioriProjectSettings { SetupCompleted = true });

            Assert.That(_dir.ReadText("ProjectSettings/Shiori.json"), Is.EqualTo("{\n  \"schemaVersion\": 1,\n  \"setupCompleted\": true\n}\n"));
            Assert.That(_store.LoadProject().SetupCompleted, Is.True);
            Assert.That(File.Exists(_store.ProjectSettingsPath + ".tmp"), Is.False);
        }

        [Test]
        public void User_RoundTrips()
        {
            _store.SaveUser(new ShioriUserSettings { GitPath = @"C:\Program Files\Git\cmd\git.exe", Mode = UiMode.Detail, Modes = ModeAvailability.SimpleOnly, LastTab = "history" });

            var loaded = _store.LoadUser();
            Assert.That(loaded.GitPath, Is.EqualTo(@"C:\Program Files\Git\cmd\git.exe"));
            Assert.That(loaded.Mode, Is.EqualTo(UiMode.Detail));
            Assert.That(loaded.Modes, Is.EqualTo(ModeAvailability.SimpleOnly));
            Assert.That(loaded.LastTab, Is.EqualTo("history"));
        }

        [Test]
        public void EffectiveMode_FollowsAvailability()
        {
            var settings = new ShioriUserSettings { Mode = UiMode.Detail };
            Assert.That(settings.EffectiveMode, Is.EqualTo(UiMode.Detail));
            Assert.That(settings.CanSwitchMode, Is.True);

            settings.Modes = ModeAvailability.SimpleOnly;
            Assert.That(settings.EffectiveMode, Is.EqualTo(UiMode.Simple));
            Assert.That(settings.CanSwitchMode, Is.False);

            settings.Modes = ModeAvailability.DetailOnly;
            settings.Mode = UiMode.Simple;
            Assert.That(settings.EffectiveMode, Is.EqualTo(UiMode.Detail));
        }

        [Test]
        public void Modes_DefaultsToBothAndIgnoresUnknownValues()
        {
            Assert.That(_store.LoadUser().Modes, Is.EqualTo(ModeAvailability.Both));
            _dir.WriteText("UserSettings/Shiori.json", "{\"modes\":\"weird\"}");
            Assert.That(_store.LoadUser().Modes, Is.EqualTo(ModeAvailability.Both));
        }

        [Test]
        public void UnknownKeysAndBadTypes_AreIgnored()
        {
            _dir.WriteText("UserSettings/Shiori.json", "{\"schemaVersion\":1,\"gitPath\":123,\"mode\":\"weird\",\"future\":{\"x\":1}}");
            var user = _store.LoadUser();
            Assert.That(user.GitPath, Is.Empty);
            Assert.That(user.Mode, Is.EqualTo(UiMode.Simple));
        }

        [Test]
        public void CorruptFile_ThrowsSettingsFormatExceptionWithPath()
        {
            _dir.WriteText("ProjectSettings/Shiori.json", "{ not json");
            var ex = Assert.Throws<SettingsFormatException>(() => _store.LoadProject());
            Assert.That(ex.FilePath, Is.EqualTo(_store.ProjectSettingsPath));

            _dir.WriteText("ProjectSettings/Shiori.json", "[1,2]");
            Assert.Throws<SettingsFormatException>(() => _store.LoadProject());
        }

        [Test]
        public void Save_OverwritesExistingFile()
        {
            _store.SaveProject(new ShioriProjectSettings { SetupCompleted = false });
            _store.SaveProject(new ShioriProjectSettings { SetupCompleted = true });
            Assert.That(_store.LoadProject().SetupCompleted, Is.True);
        }
    }
}
