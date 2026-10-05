using System.IO;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class MetaCheckerTests
    {
        private TempDirectory _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            _dir.Dispose();
        }

        [Test]
        public void NoAssetsFolder_NoIssues()
        {
            var result = MetaChecker.Check(_dir.Path);
            Assert.That(result.HasIssues, Is.False);
        }

        [Test]
        public void ConsistentProject_NoIssues()
        {
            _dir.WriteText("Assets/Scenes/Main.unity", "");
            _dir.WriteText("Assets/Scenes/Main.unity.meta", "");
            _dir.WriteText("Assets/Scenes.meta", "");
            Assert.That(MetaChecker.Check(_dir.Path).HasIssues, Is.False);
        }

        [Test]
        public void MissingMeta_ForFileAndFolder()
        {
            _dir.WriteText("Assets/Textures/a b.png", "");
            _dir.WriteText("Assets/日本語.txt", "");
            _dir.WriteText("Assets/日本語.txt.meta", "");

            var result = MetaChecker.Check(_dir.Path);

            Assert.That(result.MissingMeta, Is.EqualTo(new[] { "Assets/Textures", "Assets/Textures/a b.png" }));
            Assert.That(result.OrphanMeta, Is.Empty);
        }

        [Test]
        public void OrphanMeta_WhenAssetIsGone()
        {
            _dir.WriteText("Assets/gone.mat.meta", "");
            _dir.WriteText("Assets/Folder.meta", "");

            var result = MetaChecker.Check(_dir.Path);

            Assert.That(result.OrphanMeta, Is.EqualTo(new[] { "Assets/Folder.meta", "Assets/gone.mat.meta" }));
            Assert.That(result.MissingMeta, Is.Empty);
        }

        [Test]
        public void HiddenEntries_AreIgnoredAndTheirMetaIsOrphan()
        {
            _dir.WriteText("Assets/Backup~/x.txt", "");
            _dir.WriteText("Assets/.hidden/y.txt", "");
            _dir.WriteText("Assets/scratch.tmp", "");
            _dir.WriteText("Assets/Backup~.meta", "");
            Directory.CreateDirectory(_dir.File("Assets/cvs"));

            var result = MetaChecker.Check(_dir.Path);

            Assert.That(result.MissingMeta, Is.Empty);
            Assert.That(result.OrphanMeta, Is.EqualTo(new[] { "Assets/Backup~.meta" }));
        }

        [Test]
        public void MetaWithoutBaseName_IsOrphan()
        {
            _dir.WriteText("Assets/.meta", "");
            var result = MetaChecker.Check(_dir.Path);
            // ".meta" starts with '.', so Unity ignores it entirely.
            Assert.That(result.HasIssues, Is.False);
        }

        [TestCase(".git", true, true)]
        [TestCase("Backup~", true, true)]
        [TestCase("file~", false, true)]
        [TestCase("CVS", true, true)]
        [TestCase("cvs", false, false)]
        [TestCase("a.tmp", false, true)]
        [TestCase("a.TMP", false, true)]
        [TestCase("Normal.cs", false, false)]
        public void IsIgnoredByUnity(string name, bool isDirectory, bool expected)
        {
            Assert.That(MetaChecker.IsIgnoredByUnity(name, isDirectory), Is.EqualTo(expected));
        }
    }
}
