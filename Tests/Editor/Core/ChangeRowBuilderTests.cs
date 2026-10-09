using System.Linq;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class ChangeRowBuilderTests
    {
        private static FileChange Change(string path, ChangeKind kind = ChangeKind.Modified)
        {
            return new FileChange(path, kind);
        }

        [Test]
        public void MetaIsFoldedIntoItsAssetRow()
        {
            var rows = ChangeRowBuilder.Build(new[]
            {
                Change("Assets/Scenes/Main.unity"),
                Change("Assets/Scenes/Main.unity.meta"),
                Change("Assets/Textures/a b.png.meta", ChangeKind.Added),
                Change("Assets/Textures/a b.png", ChangeKind.Added),
            });

            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Path, Is.EqualTo("Assets/Scenes/Main.unity"));
            Assert.That(rows[0].DisplayPath, Is.EqualTo("Scenes/Main.unity"));
            Assert.That(rows[0].HasMeta, Is.True);
            Assert.That(rows[0].MetaOnly, Is.False);
            Assert.That(rows[1].DisplayPath, Is.EqualTo("Textures/a b.png"));
            Assert.That(rows[1].Kind, Is.EqualTo(ChangeKind.Added));
            Assert.That(rows[1].HasMeta, Is.True);
        }

        [Test]
        public void MetaWithoutAssetBecomesAMetaOnlyRow()
        {
            var rows = ChangeRowBuilder.Build(new[] { Change("Assets/NewFolder.meta", ChangeKind.Added) });
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Path, Is.EqualTo("Assets/NewFolder"));
            Assert.That(rows[0].DisplayPath, Is.EqualTo("NewFolder"));
            Assert.That(rows[0].MetaOnly, Is.True);
            Assert.That(rows[0].HasMeta, Is.True);
            Assert.That(rows[0].Kind, Is.EqualTo(ChangeKind.Added));
        }

        [Test]
        public void PathsOutsideAssetsKeepTheirFullPath()
        {
            var rows = ChangeRowBuilder.Build(new[] { Change("ProjectSettings/ProjectSettings.asset"), Change("Packages/manifest.json") });
            Assert.That(rows.Select(r => r.DisplayPath), Is.EqualTo(new[] { "ProjectSettings/ProjectSettings.asset", "Packages/manifest.json" }));
            Assert.That(rows.All(r => !r.HasMeta), Is.True);
        }

        [Test]
        public void OrderFollowsFirstAppearance()
        {
            var rows = ChangeRowBuilder.Build(new[] { Change("b.txt.meta"), Change("a.txt"), Change("b.txt") });
            Assert.That(rows.Select(r => r.Path), Is.EqualTo(new[] { "b.txt", "a.txt" }));
        }

        [Test]
        public void Empty()
        {
            Assert.That(ChangeRowBuilder.Build(null), Is.Empty);
            Assert.That(ChangeRowBuilder.Build(new FileChange[0]), Is.Empty);
        }
    }
}
