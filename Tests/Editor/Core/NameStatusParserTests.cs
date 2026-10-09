using NUnit.Framework;

namespace Shiori.Tests
{
    public class NameStatusParserTests
    {
        [Test]
        public void Parse_StatusPathPairs()
        {
            var changes = NameStatusParser.Parse("M\0Assets/a b.txt\0A\0Assets/日本語.txt\0D\0gone.txt\0T\0link\0");
            Assert.That(changes.Count, Is.EqualTo(4));
            Assert.That(changes[0].Kind, Is.EqualTo(ChangeKind.Modified));
            Assert.That(changes[0].Path, Is.EqualTo("Assets/a b.txt"));
            Assert.That(changes[1].Kind, Is.EqualTo(ChangeKind.Added));
            Assert.That(changes[1].Path, Is.EqualTo("Assets/日本語.txt"));
            Assert.That(changes[2].Kind, Is.EqualTo(ChangeKind.Deleted));
            Assert.That(changes[3].Kind, Is.EqualTo(ChangeKind.TypeChanged));
        }

        [Test]
        public void Parse_RenameAndCopyConsumeTwoPaths()
        {
            var changes = NameStatusParser.Parse("R100\0old.txt\0new.txt\0C075\0src.txt\0copy.txt\0M\0x\0");
            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[0].Kind, Is.EqualTo(ChangeKind.Renamed));
            Assert.That(changes[0].OldPath, Is.EqualTo("old.txt"));
            Assert.That(changes[0].Path, Is.EqualTo("new.txt"));
            Assert.That(changes[1].Kind, Is.EqualTo(ChangeKind.Copied));
            Assert.That(changes[1].OldPath, Is.EqualTo("src.txt"));
            Assert.That(changes[1].Path, Is.EqualTo("copy.txt"));
            Assert.That(changes[2].Path, Is.EqualTo("x"));
        }

        [Test]
        public void Parse_Empty()
        {
            Assert.That(NameStatusParser.Parse(""), Is.Empty);
        }

        [Test]
        public void Parse_TruncatedOutputDoesNotThrow()
        {
            Assert.That(NameStatusParser.Parse("M\0"), Is.Empty);
            Assert.That(NameStatusParser.Parse("R100\0old\0"), Is.Empty);
        }

        [Test]
        public void StashList_ParsesSelectorHashSubjectAndTime()
        {
            var entries = StashListParser.Parse("stash@{0}\0f87d2e70e7484dac858f587b1aacdbff547f667c\0On main: shiori:auto-before-restore: first\01791554195\0stash@{1}\0abc\0WIP on main: x\0garbage\0");
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].Selector, Is.EqualTo("stash@{0}"));
            Assert.That(entries[0].Hash, Is.EqualTo("f87d2e70e7484dac858f587b1aacdbff547f667c"));
            Assert.That(entries[0].Message, Is.EqualTo("On main: shiori:auto-before-restore: first"));
            Assert.That(entries[0].Time, Is.EqualTo(System.DateTimeOffset.FromUnixTimeSeconds(1791554195)));
            Assert.That(entries[1].Selector, Is.EqualTo("stash@{1}"));
            Assert.That(entries[1].Time, Is.EqualTo(System.DateTimeOffset.MinValue), "an unreadable time is unknown, not an error");
        }
    }
}
