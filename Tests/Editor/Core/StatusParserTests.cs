using NUnit.Framework;

namespace Shiori.Tests
{
    public class StatusParserTests
    {
        private const string Hash = "f01f1197bcb6ce4715db52acbe48a69589e5aeec";
        private const string Blob = "c59d9b6344f1af00e504ba698129f07a34bbed8d";

        [Test]
        public void Parse_EmptyOutput_HasNoChanges()
        {
            var status = StatusParser.Parse("");
            Assert.That(status.HasChanges, Is.False);
            Assert.That(status.Branch, Is.Null);
            Assert.That(status.HeadHash, Is.Null);
        }

        [Test]
        public void Parse_ReadsBranchHeaders()
        {
            var status = StatusParser.Parse($"# branch.oid {Hash}\0# branch.head main\0");
            Assert.That(status.Branch, Is.EqualTo("main"));
            Assert.That(status.HeadHash, Is.EqualTo(Hash));
            Assert.That(status.IsDetached, Is.False);
        }

        [Test]
        public void Parse_InitialAndDetachedHeaders()
        {
            var status = StatusParser.Parse("# branch.oid (initial)\0# branch.head (detached)\0");
            Assert.That(status.HeadHash, Is.Null);
            Assert.That(status.Branch, Is.Null);
            Assert.That(status.IsDetached, Is.True);
        }

        [Test]
        public void Parse_OrdinaryEntries_WithSpacesJapaneseAndNewlines()
        {
            var output =
                $"1 .M N... 100644 100644 100644 {Blob} {Blob} Assets/a b.txt\0" +
                $"1 A. N... 000000 100644 100644 0000000000000000000000000000000000000000 {Blob} Assets/日本語.txt\0" +
                $"1 .D N... 100644 100644 000000 {Blob} {Blob} Assets/line\nbreak.txt\0" +
                "? Assets/new file.meta\0";

            var status = StatusParser.Parse(output);

            Assert.That(status.Changes.Count, Is.EqualTo(4));
            Assert.That(status.Changes[0].Path, Is.EqualTo("Assets/a b.txt"));
            Assert.That(status.Changes[0].Kind, Is.EqualTo(ChangeKind.Modified));
            Assert.That(status.Changes[0].IndexStatus, Is.EqualTo('.'));
            Assert.That(status.Changes[0].WorktreeStatus, Is.EqualTo('M'));
            Assert.That(status.Changes[1].Path, Is.EqualTo("Assets/日本語.txt"));
            Assert.That(status.Changes[1].Kind, Is.EqualTo(ChangeKind.Added));
            Assert.That(status.Changes[2].Path, Is.EqualTo("Assets/line\nbreak.txt"));
            Assert.That(status.Changes[2].Kind, Is.EqualTo(ChangeKind.Deleted));
            Assert.That(status.Changes[3].Path, Is.EqualTo("Assets/new file.meta"));
            Assert.That(status.Changes[3].Kind, Is.EqualTo(ChangeKind.Untracked));
            Assert.That(status.Changes[3].IsMeta, Is.True);

            Assert.That(status.Stats.Added, Is.EqualTo(2));
            Assert.That(status.Stats.Modified, Is.EqualTo(1));
            Assert.That(status.Stats.Deleted, Is.EqualTo(1));
            Assert.That(status.Stats.Total, Is.EqualTo(4));
        }

        [Test]
        public void Parse_RenameEntry_TakesOriginalPathFromNextToken()
        {
            var output = $"2 R. N... 100644 100644 100644 {Blob} {Blob} R100 renamed dir/renamed.txt\0old dir/new.txt\0";
            var status = StatusParser.Parse(output);

            Assert.That(status.Changes.Count, Is.EqualTo(1));
            var change = status.Changes[0];
            Assert.That(change.Kind, Is.EqualTo(ChangeKind.Renamed));
            Assert.That(change.Path, Is.EqualTo("renamed dir/renamed.txt"));
            Assert.That(change.OldPath, Is.EqualTo("old dir/new.txt"));
        }

        [Test]
        public void Parse_UnmergedEntry()
        {
            var output = $"u UU N... 100644 100644 100644 100644 {Blob} {Blob} {Blob} Assets/conflict.unity\0";
            var status = StatusParser.Parse(output);
            Assert.That(status.Changes[0].Kind, Is.EqualTo(ChangeKind.Unmerged));
            Assert.That(status.Changes[0].Path, Is.EqualTo("Assets/conflict.unity"));
        }

        [TestCase('.', 'M', ChangeKind.Modified)]
        [TestCase('M', '.', ChangeKind.Modified)]
        [TestCase('M', 'M', ChangeKind.Modified)]
        [TestCase('A', '.', ChangeKind.Added)]
        [TestCase('A', 'M', ChangeKind.Added)]
        [TestCase('A', 'D', ChangeKind.Deleted)]
        [TestCase('.', 'D', ChangeKind.Deleted)]
        [TestCase('D', '.', ChangeKind.Deleted)]
        [TestCase('R', '.', ChangeKind.Renamed)]
        [TestCase('.', 'T', ChangeKind.TypeChanged)]
        [TestCase('U', 'U', ChangeKind.Unmerged)]
        public void Classify_CollapsesIndexAndWorktree(char x, char y, ChangeKind expected)
        {
            Assert.That(StatusParser.Classify(x, y), Is.EqualTo(expected));
        }
    }
}
