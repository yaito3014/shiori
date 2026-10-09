using NUnit.Framework;

namespace Shiori.Tests
{
    /// <summary>Several packages own one block each in the same file.</summary>
    public class ManagedBlockIdTests
    {
        private static readonly string[] Core = { "[Ll]ibrary/", "*.csproj" };
        private static readonly string[] Ext = { "Packages/com.vrchat.base/", "Packages/com.vrchat.avatars/" };
        private static readonly string[] Ext2 = { "Packages/com.vrchat.base/" };

        [Test]
        public void DefaultBlockId_UsesTheHistoricalMarkers()
        {
            Assert.That(ManagedBlockWriter.StartMarkerFor(ManagedBlockWriter.DefaultBlockId), Is.EqualTo(ManagedBlockWriter.StartMarker));
            Assert.That(ManagedBlockWriter.EndMarkerFor(ManagedBlockWriter.DefaultBlockId), Is.EqualTo(ManagedBlockWriter.EndMarker));
            Assert.That(ManagedBlockWriter.Upsert(null, Core), Is.EqualTo(ManagedBlockWriter.Upsert(null, "shiori", Core)));
        }

        [Test]
        public void TwoBlocks_CoexistAndUpdateIndependently()
        {
            var content = ManagedBlockWriter.Upsert("user\n", Core);
            content = ManagedBlockWriter.Upsert(content, "shiori-vrchat", Ext);

            Assert.That(ManagedBlockWriter.ReadBlock(content), Is.EqualTo(Core));
            Assert.That(ManagedBlockWriter.ReadBlock(content, "shiori-vrchat"), Is.EqualTo(Ext));
            Assert.That(content, Does.StartWith("user\n"));
            Assert.That(content.IndexOf(ManagedBlockWriter.StartMarker), Is.LessThan(content.IndexOf(ManagedBlockWriter.StartMarkerFor("shiori-vrchat"))));

            var updated = ManagedBlockWriter.Upsert(content, "shiori-vrchat", Ext2);
            Assert.That(ManagedBlockWriter.ReadBlock(updated), Is.EqualTo(Core), "core block untouched");
            Assert.That(ManagedBlockWriter.ReadBlock(updated, "shiori-vrchat"), Is.EqualTo(Ext2));
            Assert.That(ManagedBlockWriter.Upsert(updated, "shiori-vrchat", Ext2), Is.EqualTo(updated), "idempotent");

            var coreUpdated = ManagedBlockWriter.Upsert(updated, new[] { "*.sln" });
            Assert.That(ManagedBlockWriter.ReadBlock(coreUpdated), Is.EqualTo(new[] { "*.sln" }));
            Assert.That(ManagedBlockWriter.ReadBlock(coreUpdated, "shiori-vrchat"), Is.EqualTo(Ext2), "extension block untouched");
        }

        [Test]
        public void SimilarIds_DoNotMatchEachOther()
        {
            var content = ManagedBlockWriter.Upsert(null, "shiori-vrchat", Ext);
            Assert.That(ManagedBlockWriter.ReadBlock(content), Is.Null, "the core block is not confused with shiori-vrchat");
            Assert.That(ManagedBlockWriter.ReadBlock(content, "shiori-vr"), Is.Null);
        }

        [Test]
        public void InvalidBlockId_IsRejected()
        {
            Assert.That(() => ManagedBlockWriter.Upsert(null, "", Core), Throws.ArgumentException);
            Assert.That(() => ManagedBlockWriter.Upsert(null, "has space", Core), Throws.ArgumentException);
            Assert.That(() => ManagedBlockWriter.Upsert(null, "new\nline", Core), Throws.ArgumentException);
            Assert.That(() => ManagedBlockWriter.ReadBlock("x", "a/b"), Throws.ArgumentException);
            Assert.That(ManagedBlockWriter.ValidateBlockId("com.yaito3014.shiori_x-1"), Is.EqualTo("com.yaito3014.shiori_x-1"));
        }
    }
}
