using NUnit.Framework;

namespace Shiori.Tests
{
    public class StagePathsTests
    {
        private static FileChange Untracked(string path) => new FileChange(path, ChangeKind.Untracked, '?', '?');
        private static FileChange Modified(string path) => new FileChange(path, ChangeKind.Modified, '.', 'M');

        [Test]
        public void AnAssetAndItsMeta_MoveTogether_InEitherDirection()
        {
            var list = new[] { Modified("Assets/a.mat"), Modified("Assets/a.mat.meta"), Modified("Assets/b.mat") };

            Assert.That(StagePaths.For(new[] { list[0] }, list, staging: true), Is.EqualTo(new[] { "Assets/a.mat", "Assets/a.mat.meta" }));
            Assert.That(StagePaths.For(new[] { list[1] }, list, staging: true), Is.EqualTo(new[] { "Assets/a.mat.meta", "Assets/a.mat" }));
            Assert.That(StagePaths.For(new[] { list[2] }, list, staging: true), Is.EqualTo(new[] { "Assets/b.mat" }), "no partner in the list, nothing added");
        }

        [Test]
        public void StagingAFileInANewFolder_BringsTheFolderMeta_OnlyWhenTheFolderIsNew()
        {
            var list = new[]
            {
                Untracked("Assets/New/Deep/a.png"),
                Untracked("Assets/New/Deep.meta"),
                Untracked("Assets/New.meta"),
                Modified("Assets/Old.meta"),
                Modified("Assets/Old/b.png"),
            };

            Assert.That(StagePaths.For(new[] { list[0] }, list, staging: true),
                Is.EqualTo(new[] { "Assets/New/Deep/a.png", "Assets/New/Deep.meta", "Assets/New.meta" }));
            Assert.That(StagePaths.For(new[] { list[4] }, list, staging: true), Is.EqualTo(new[] { "Assets/Old/b.png" }),
                "an existing folder's .meta changed on its own and stays where it is");
            Assert.That(StagePaths.For(new[] { list[0] }, list, staging: false), Is.EqualTo(new[] { "Assets/New/Deep/a.png" }),
                "unstaging a file leaves its folder staged");
        }

        [Test]
        public void UnstagingARename_BringsTheOldPathBack_StagingDoesNot()
        {
            var rename = new FileChange("Assets/new.txt", ChangeKind.Renamed, 'R', 'M', "Assets/old.txt");
            var list = new[] { rename };

            Assert.That(StagePaths.For(list, list, staging: false), Is.EqualTo(new[] { "Assets/new.txt", "Assets/old.txt" }));
            Assert.That(StagePaths.For(list, list, staging: true), Is.EqualTo(new[] { "Assets/new.txt" }),
                "the old path no longer exists on disk; git add would reject it");
        }

        [Test]
        public void EachPathIsListedOnce()
        {
            var list = new[] { Modified("Assets/a.mat"), Modified("Assets/a.mat.meta") };
            Assert.That(StagePaths.For(list, list, staging: true), Is.EqualTo(new[] { "Assets/a.mat", "Assets/a.mat.meta" }));
        }

        [Test]
        public void StagedAndUnstagedSides_FollowTheStatusLetters()
        {
            Assert.That(new FileChange("a", ChangeKind.Modified, 'M', '.').IsStaged, Is.True);
            Assert.That(new FileChange("a", ChangeKind.Modified, 'M', '.').IsUnstaged, Is.False);
            Assert.That(new FileChange("a", ChangeKind.Modified, 'M', 'M').IsUnstaged, Is.True);
            Assert.That(Untracked("a").IsStaged, Is.False);
            Assert.That(Untracked("a").IsUnstaged, Is.True);
        }
    }
}
