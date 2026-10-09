using NUnit.Framework;

namespace Shiori.Tests
{
    public class SnapshotMessageTests
    {
        [Test]
        public void Build_UsesTheFixedEnglishTemplate()
        {
            Assert.That(SnapshotMessage.Build(new ChangeStats(2, 3, 1)),
                Is.EqualTo("Snapshot: 6 files changed (2 added, 3 modified, 1 deleted)"));
        }

        [Test]
        public void Resolve_PrefersTheTrimmedMemo()
        {
            var stats = new ChangeStats(1, 0, 0);
            Assert.That(SnapshotMessage.Resolve("  衣装を着せ替える前  ", stats), Is.EqualTo("衣装を着せ替える前"));
            Assert.That(SnapshotMessage.Resolve("   ", stats), Is.EqualTo("Snapshot: 1 files changed (1 added, 0 modified, 0 deleted)"));
            Assert.That(SnapshotMessage.Resolve(null, stats), Does.StartWith("Snapshot:"));
        }
    }
}
