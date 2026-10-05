using System;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class LogParserTests
    {
        private const string H1 = "38190eaccbf790eee452b0a66751b2034f987666";
        private const string H2 = "2c3843171656458b1b312e91e0e531e90890a7ca1";
        private const string H3 = "4c0661ba08a388cea49ee0e9433e821f9861fe1a";

        // Exactly what `git log -z --format=%x1e%H%x00%at%x00%s%x00%an --name-status` printed in a probe.
        private static readonly string Sample =
            $"\x1e{H1}\01791192474\0third\0T\0\nD\0new.txt\0A\0untracked file.txt\0M\0日本語.txt\0" +
            $"\x1e{H2}\01791192474\0empty\0T\0" +
            $"\x1e{H3}\01791192383\0first\0Author Name\0\nA\0a b.txt\0R100\0old.txt\0new\nline.txt\0";

        [Test]
        public void Parse_Empty_ReturnsNoSnapshots()
        {
            Assert.That(LogParser.Parse(""), Is.Empty);
            Assert.That(LogParser.Parse(null), Is.Empty);
        }

        [Test]
        public void Parse_ReadsHeadersInOrder()
        {
            var log = LogParser.Parse(Sample);

            Assert.That(log.Count, Is.EqualTo(3));
            Assert.That(log[0].Hash, Is.EqualTo(H1));
            Assert.That(log[0].ShortHash, Is.EqualTo("38190ea"));
            Assert.That(log[0].Message, Is.EqualTo("third"));
            Assert.That(log[0].Author, Is.EqualTo("T"));
            Assert.That(log[0].Time, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1791192474)));
            Assert.That(log[2].Author, Is.EqualTo("Author Name"));
        }

        [Test]
        public void Parse_AttachesChangedFilesToTheRightCommit()
        {
            var log = LogParser.Parse(Sample);

            Assert.That(log[0].Changes.Count, Is.EqualTo(3));
            Assert.That(log[0].Changes[0].Kind, Is.EqualTo(ChangeKind.Deleted));
            Assert.That(log[0].Changes[0].Path, Is.EqualTo("new.txt"));
            Assert.That(log[0].Changes[1].Path, Is.EqualTo("untracked file.txt"));
            Assert.That(log[0].Changes[2].Path, Is.EqualTo("日本語.txt"));
            Assert.That(log[0].Stats.Added, Is.EqualTo(1));
            Assert.That(log[0].Stats.Modified, Is.EqualTo(1));
            Assert.That(log[0].Stats.Deleted, Is.EqualTo(1));

            Assert.That(log[1].Changes, Is.Empty, "a commit without file changes has no trailing file tokens");

            Assert.That(log[2].Changes.Count, Is.EqualTo(2));
            Assert.That(log[2].Changes[0].Path, Is.EqualTo("a b.txt"));
            Assert.That(log[2].Changes[1].Kind, Is.EqualTo(ChangeKind.Renamed));
            Assert.That(log[2].Changes[1].OldPath, Is.EqualTo("old.txt"));
            Assert.That(log[2].Changes[1].Path, Is.EqualTo("new\nline.txt"));
        }

        [Test]
        public void Parse_SubjectMayContainSpacesAndJapanese()
        {
            var log = LogParser.Parse($"\x1e{H1}\00\0衣装を 着せ替え: 前\0作者\0");
            Assert.That(log[0].Message, Is.EqualTo("衣装を 着せ替え: 前"));
            Assert.That(log[0].Author, Is.EqualTo("作者"));
            Assert.That(log[0].Time, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(0)));
        }
    }
}
