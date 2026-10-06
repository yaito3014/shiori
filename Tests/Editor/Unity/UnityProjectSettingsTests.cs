using NUnit.Framework;
using UnityEditor;

namespace Shiori.Editor.Tests
{
    public class UnityProjectSettingsTests
    {
        private SerializationMode _originalSerialization;
        private string _originalVersionControl;

        [SetUp]
        public void SetUp()
        {
            _originalSerialization = EditorSettings.serializationMode;
            _originalVersionControl = VersionControlSettings.mode;
        }

        [TearDown]
        public void TearDown()
        {
            EditorSettings.serializationMode = _originalSerialization;
            VersionControlSettings.mode = _originalVersionControl;
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void Apply_SetsForceTextAndVisibleMetaFiles()
        {
            EditorSettings.serializationMode = SerializationMode.ForceBinary;
            VersionControlSettings.mode = "Hidden Meta Files";
            Assert.That(UnityProjectSettings.IsForceText, Is.False);
            Assert.That(UnityProjectSettings.IsVisibleMetaFiles, Is.False);
            Assert.That(UnityProjectSettings.IsConfigured, Is.False);

            UnityProjectSettings.Apply();

            Assert.That(UnityProjectSettings.IsForceText, Is.True);
            Assert.That(UnityProjectSettings.IsVisibleMetaFiles, Is.True);
            Assert.That(UnityProjectSettings.IsConfigured, Is.True);
            Assert.That(UnityProjectSettings.SerializationModeName, Is.EqualTo("ForceText"));
            Assert.That(UnityProjectSettings.VersionControlModeName, Is.EqualTo("Visible Meta Files"));
        }

        [Test]
        public void Apply_IsIdempotent()
        {
            UnityProjectSettings.Apply();
            UnityProjectSettings.Apply();
            Assert.That(UnityProjectSettings.IsConfigured, Is.True);
        }
    }
}
