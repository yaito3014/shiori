using NUnit.Framework;

namespace Shiori.Tests
{
    public class VariationNamesTests
    {
        [TestCase("衣装A", "shiori/衣装A")]
        [TestCase("  夜 の 部屋  ", "shiori/夜-の-部屋")]
        [TestCase("a/b", "shiori/a-b")]
        [TestCase("what?*[x]", "shiori/what-x]")]
        [TestCase("v1..2", "shiori/v1.2")]
        [TestCase(".hidden.", "shiori/hidden")]
        [TestCase("name.lock", "shiori/name")]
        [TestCase("a@{1}", "shiori/a@-1}")]
        public void ToBranch_KeepsWhatGitAllows(string typed, string expected)
        {
            Assert.That(VariationNames.ToBranch(typed), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("///")]
        [TestCase("@")]
        public void ToBranch_IsNullWhenNothingIsLeft(string typed)
        {
            Assert.That(VariationNames.ToBranch(typed), Is.Null);
        }

        [Test]
        public void Display_DropsThePrefix_AndKeepsOtherNames()
        {
            Assert.That(VariationNames.Display("shiori/衣装A"), Is.EqualTo("衣装A"));
            Assert.That(VariationNames.Display("feature/x"), Is.EqualTo("feature/x"));
            Assert.That(VariationNames.Display("shiori/"), Is.EqualTo("shiori/"));
            Assert.That(VariationNames.IsMain("main") && VariationNames.IsMain("master"), Is.True);
            Assert.That(VariationNames.IsMain("shiori/main"), Is.False);
        }

        [Test]
        public void BranchParser_ListsLocalsThenRemoteOnly_AndSkipsOriginHead()
        {
            var output =
                "refs/heads/main\0aaa\0*\01700000000\0first save\n" +
                "refs/heads/shiori/衣装A\0bbb\0 \01700000100\0tab\tin subject\n" +
                "refs/remotes/origin/HEAD\0aaa\0 \01700000000\0first save\n" +
                "refs/remotes/origin/main\0aaa\0 \01700000000\0first save\n" +
                "refs/remotes/origin/shiori/夜\0ccc\0 \01700000200\0night\n";

            var branches = BranchParser.Parse(output);

            Assert.That(branches.Count, Is.EqualTo(3));
            Assert.That(branches[0].Name, Is.EqualTo("main"));
            Assert.That(branches[0].IsCurrent && branches[0].IsOnRemote && !branches[0].IsRemoteOnly, Is.True);
            Assert.That(branches[1].Name, Is.EqualTo("shiori/衣装A"));
            Assert.That(branches[1].IsCurrent || branches[1].IsOnRemote, Is.False);
            Assert.That(branches[1].Subject, Is.EqualTo("tab\tin subject"));
            Assert.That(branches[1].RefName, Is.EqualTo("refs/heads/shiori/衣装A"));
            Assert.That(branches[2].Name, Is.EqualTo("shiori/夜"));
            Assert.That(branches[2].IsRemoteOnly, Is.True);
            Assert.That(branches[2].RefName, Is.EqualTo("refs/remotes/origin/shiori/夜"));
            Assert.That(branches[2].Time.ToUnixTimeSeconds(), Is.EqualTo(1700000200));
        }

        [Test]
        public void OverwriteRefusal_ListsTheBlockingFiles()
        {
            var stderr = "error: Your local changes to the following files would be overwritten by checkout:\n\tAssets/a.txt\n\tAssets/b c.txt\nPlease commit your changes or stash them before you switch branches.\nAborting\n";
            Assert.That(VariationRunner.IsOverwriteRefusal(stderr), Is.True);
            Assert.That(VariationRunner.ListedPaths(stderr), Is.EqualTo(new[] { "Assets/a.txt", "Assets/b c.txt" }));
            Assert.That(VariationRunner.IsOverwriteRefusal("fatal: invalid reference: x"), Is.False);
        }
    }
}
