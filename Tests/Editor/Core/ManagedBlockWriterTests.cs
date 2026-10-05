using NUnit.Framework;

namespace Shiori.Tests
{
    public class ManagedBlockWriterTests
    {
        private static readonly string[] LinesA = { "[Ll]ibrary/", "*.csproj" };
        private static readonly string[] LinesB = { "[Ll]ibrary/", "[Tt]emp/", "*.sln" };

        private const string Start = ManagedBlockWriter.StartMarker;
        private const string End = ManagedBlockWriter.EndMarker;

        [Test]
        public void Upsert_NoFile_CreatesBlockWithLf()
        {
            var result = ManagedBlockWriter.Upsert(null, LinesA);
            Assert.That(result, Is.EqualTo($"{Start}\n[Ll]ibrary/\n*.csproj\n{End}\n"));
        }

        [Test]
        public void Upsert_FileWithoutBlock_AppendsAfterBlankLineAndKeepsUserLines()
        {
            var existing = "# mine\nLibrary/\n";
            var result = ManagedBlockWriter.Upsert(existing, LinesA);
            Assert.That(result, Is.EqualTo($"# mine\nLibrary/\n\n{Start}\n[Ll]ibrary/\n*.csproj\n{End}\n"));
        }

        [Test]
        public void Upsert_FileWithoutTrailingNewline_StillSeparates()
        {
            var result = ManagedBlockWriter.Upsert("Library/", LinesA);
            Assert.That(result, Does.StartWith("Library/\n\n" + Start));
        }

        [Test]
        public void Upsert_ExistingBlock_IsReplacedInPlace()
        {
            var first = ManagedBlockWriter.Upsert("# head\n", LinesA) + "\n# tail\n";
            var second = ManagedBlockWriter.Upsert(first, LinesB);
            Assert.That(second, Is.EqualTo($"# head\n\n{Start}\n[Ll]ibrary/\n[Tt]emp/\n*.sln\n{End}\n\n# tail\n"));
        }

        [Test]
        public void Upsert_IsIdempotent()
        {
            var once = ManagedBlockWriter.Upsert("user\n", LinesA);
            var twice = ManagedBlockWriter.Upsert(once, LinesA);
            Assert.That(twice, Is.EqualTo(once));
        }

        [Test]
        public void Upsert_PreservesCrlf()
        {
            var existing = "# mine\r\nLibrary/\r\n";
            var result = ManagedBlockWriter.Upsert(existing, LinesA);
            Assert.That(result, Is.EqualTo($"# mine\r\nLibrary/\r\n\r\n{Start}\r\n[Ll]ibrary/\r\n*.csproj\r\n{End}\r\n"));
            Assert.That(result, Does.Not.Contain("\n\n"), "no bare LF line endings were introduced");

            var updated = ManagedBlockWriter.Upsert(result, LinesB);
            Assert.That(updated, Does.Contain("\r\n[Tt]emp/\r\n"));
            Assert.That(updated.Replace("\r\n", ""), Does.Not.Contain("\n"));
        }

        [Test]
        public void Upsert_MarkerInsideOtherTextIsNotTreatedAsBlock()
        {
            var existing = "# not a marker: " + Start + "\n";
            var result = ManagedBlockWriter.Upsert(existing, LinesA);
            Assert.That(result, Does.StartWith(existing));
            Assert.That(result, Does.EndWith(End + "\n"));
        }

        [Test]
        public void ReadBlock_ReturnsInnerLines()
        {
            var content = ManagedBlockWriter.Upsert("x\n", LinesB);
            Assert.That(ManagedBlockWriter.ReadBlock(content), Is.EqualTo(LinesB));
            Assert.That(ManagedBlockWriter.ReadBlock("nothing here\n"), Is.Null);
        }

        [Test]
        public void UpsertFile_CreatesUpdatesAndReportsChanges()
        {
            using (var dir = new TempDirectory())
            {
                var path = dir.File(".gitignore");
                Assert.That(ManagedBlockWriter.UpsertFile(path, LinesA), Is.True);
                Assert.That(ManagedBlockWriter.UpsertFile(path, LinesA), Is.False, "unchanged content is not rewritten");
                Assert.That(ManagedBlockWriter.UpsertFile(path, LinesB), Is.True);
                Assert.That(ManagedBlockWriter.ReadBlock(dir.ReadText(".gitignore")), Is.EqualTo(LinesB));
            }
        }

        [Test]
        public void ShioriBlocks_ContainTheSpecifiedEntries()
        {
            Assert.That(ShioriBlocks.GitIgnore, Does.Contain("[Ll]ibrary/"));
            Assert.That(ShioriBlocks.GitIgnore, Does.Contain("[Uu]ser[Ss]ettings/"));
            Assert.That(ShioriBlocks.GitAttributes, Does.Contain("*.unity text merge=unityyamlmerge"));
            Assert.That(ShioriBlocks.GitAttributes, Does.Contain("*.meta text"));
        }
    }
}
