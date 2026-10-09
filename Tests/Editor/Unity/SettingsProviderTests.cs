using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Shiori.Editor.Tests
{
    public class SettingsProviderTests
    {
        [Test]
        public void Providers_AreRegisteredAtTheExpectedPaths()
        {
            var preferences = ShioriSettingsProviders.CreatePreferencesProvider();
            Assert.That(preferences.settingsPath, Is.EqualTo("Preferences/Shiori"));
            Assert.That(preferences.scope, Is.EqualTo(SettingsScope.User));

            var project = ShioriSettingsProviders.CreateProjectProvider();
            Assert.That(project.settingsPath, Is.EqualTo("Project/Shiori"));
            Assert.That(project.scope, Is.EqualTo(SettingsScope.Project));
        }

        [Test]
        public void Pages_BuildWithoutThrowing()
        {
            var root = new VisualElement();
            Assert.DoesNotThrow(() => ShioriSettingsProviders.BuildPreferences(root));
            Assert.That(root.Q<TextField>(), Is.Not.Null, "the git path field exists");
            Assert.That(root.Q<DropdownField>(), Is.Not.Null, "the mode-availability dropdown exists");

            var projectRoot = new VisualElement();
            Assert.DoesNotThrow(() => ShioriSettingsProviders.BuildProject(projectRoot));
            Assert.That(projectRoot.Q<Toggle>(), Is.Not.Null, "the setup-completed toggle exists");
        }
    }
}
