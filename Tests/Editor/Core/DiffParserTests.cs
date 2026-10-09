using System.Linq;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class DiffParserTests
    {
        private const string Sample =
            "diff --git a/Assets/a.txt b/Assets/a.txt\n" +
            "index 3b18e51..9c3d8b2 100644\n" +
            "--- a/Assets/a.txt\n" +
            "+++ b/Assets/a.txt\n" +
            "@@ -1,3 +1,4 @@\n" +
            " line1\n" +
            "-line2\n" +
            "+line2 changed\n" +
            "+--- not a header, a removed-looking added line\n" +
            " line3\n" +
            "\\ No newline at end of file\n";

        [Test]
        public void Parse_ClassifiesHeaderHunkAndBody()
        {
            var lines = DiffParser.Parse(Sample);
            Assert.That(lines.Select(l => l.Kind), Is.EqualTo(new[]
            {
                DiffLineKind.Header, DiffLineKind.Header, DiffLineKind.Header, DiffLineKind.Header,
                DiffLineKind.Hunk,
                DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added, DiffLineKind.Added, DiffLineKind.Context,
                DiffLineKind.Meta,
            }));
            Assert.That(lines[6].Text, Is.EqualTo("-line2"));
            Assert.That(lines[8].Text, Does.StartWith("+---"));
        }

        [Test]
        public void Parse_HandlesCrlfAndEmptyInput()
        {
            Assert.That(DiffParser.Parse(""), Is.Empty);
            Assert.That(DiffParser.Parse(null), Is.Empty);
            var lines = DiffParser.Parse("@@ -1 +1 @@\r\n-a\r\n+b\r\n");
            Assert.That(lines.Select(l => l.Text), Is.EqualTo(new[] { "@@ -1 +1 @@", "-a", "+b" }));
        }

        [Test]
        public void Parse_SecondFileResetsToHeaderState()
        {
            var text = "@@ -1 +1 @@\n-a\n+b\ndiff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n+c\n";
            var lines = DiffParser.Parse(text);
            Assert.That(lines[3].Kind, Is.EqualTo(DiffLineKind.Header));
            Assert.That(lines[4].Kind, Is.EqualTo(DiffLineKind.Header), "--- after a diff line is a header, not a removal");
            Assert.That(lines[7].Kind, Is.EqualTo(DiffLineKind.Added));
        }
    }
}
