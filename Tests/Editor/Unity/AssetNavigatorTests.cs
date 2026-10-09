using NUnit.Framework;

namespace Shiori.Editor.Tests
{
    public class AssetNavigatorTests
    {
        [TestCase("Assets/Scenes/Main.unity", "Assets/Scenes/Main.unity")]
        [TestCase("Assets/Scenes/Main.unity.meta", "Assets/Scenes/Main.unity")]
        [TestCase("Assets/Folder.meta", "Assets/Folder")]
        [TestCase("Packages/com.example/Runtime/x.cs", "Packages/com.example/Runtime/x.cs")]
        [TestCase("ProjectSettings/ProjectSettings.asset", null)]
        [TestCase(".gitignore", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void ToAssetPath(string repositoryPath, string expected)
        {
            Assert.That(AssetNavigator.ToAssetPath(repositoryPath), Is.EqualTo(expected));
        }

        [Test]
        public void Reveal_ReturnsFalseForMissingOrNonAssetPaths()
        {
            Assert.That(AssetNavigator.Reveal("Assets/definitely-missing-" + System.Guid.NewGuid().ToString("N") + ".txt"), Is.False);
            Assert.That(AssetNavigator.Reveal("ProjectSettings/ProjectSettings.asset"), Is.False);
        }
    }
}
