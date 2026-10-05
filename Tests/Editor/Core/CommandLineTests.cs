using NUnit.Framework;

namespace Shiori.Tests
{
    public class CommandLineTests
    {
        [TestCase("plain", "plain")]
        [TestCase("", "\"\"")]
        [TestCase("has space", "\"has space\"")]
        [TestCase("日本語 パス", "\"日本語 パス\"")]
        [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [TestCase("C:\\path\\", "C:\\path\\")]
        [TestCase("C:\\my path\\", "\"C:\\my path\\\\\"")]
        [TestCase("back\\\"slash", "\"back\\\\\\\"slash\"")]
        [TestCase("line\nbreak", "\"line\nbreak\"")]
        public void Quote_FollowsMsvcrtRules(string input, string expected)
        {
            Assert.That(CommandLine.Quote(input), Is.EqualTo(expected));
        }

        [Test]
        public void Join_SeparatesWithSingleSpaces()
        {
            Assert.That(CommandLine.Join(new[] { "commit", "-m", "two words" }), Is.EqualTo("commit -m \"two words\""));
        }
    }
}
