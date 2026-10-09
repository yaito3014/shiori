using System;
using NUnit.Framework;

namespace Shiori.Editor.Tests
{
    public class SetupStatusTests
    {
        private static GitLocation GoodGit => new GitLocation(@"C:\git\git.exe", new Version(2, 47, 1), "git version 2.47.1", null, null);
        private static GitLocation OldGit => new GitLocation(@"C:\git\git.exe", new Version(2, 20, 0), "git version 2.20.0", null, null);

        [Test]
        public void Fresh_StartsAtGitStep_AndNothingElseIsEnabled()
        {
            var status = new SetupStatus();
            Assert.That(status.CurrentStep, Is.EqualTo(SetupStatus.GitStep));
            Assert.That(status.IsStepEnabled(SetupStatus.GitStep), Is.True);
            Assert.That(status.IsStepEnabled(SetupStatus.ProjectSettingsStep), Is.False);
            Assert.That(status.IsStepEnabled(status.FirstSaveStep), Is.False);
            Assert.That(status.IsComplete, Is.False);
        }

        [Test]
        public void MissingOrOldGit_BlocksEverything()
        {
            Assert.That(new SetupStatus { Git = GitLocation.NotFound(null) }.GitOk, Is.False);
            var old = new SetupStatus { Git = OldGit, ProjectSettingsOk = true, IgnoreFilesOk = true };
            Assert.That(old.GitOk, Is.False);
            Assert.That(old.CurrentStep, Is.EqualTo(SetupStatus.GitStep));
            Assert.That(old.IsStepEnabled(SetupStatus.ProjectSettingsStep), Is.False);
        }

        [Test]
        public void Steps_UnlockInOrder()
        {
            var status = new SetupStatus { Git = GoodGit };
            Assert.That(status.CurrentStep, Is.EqualTo(SetupStatus.ProjectSettingsStep));
            Assert.That(status.IsStepEnabled(SetupStatus.IgnoreFilesStep), Is.False);

            status.ProjectSettingsOk = true;
            Assert.That(status.CurrentStep, Is.EqualTo(SetupStatus.IgnoreFilesStep));
            Assert.That(status.IsStepEnabled(status.FirstSaveStep), Is.False);

            status.IgnoreFilesOk = true;
            Assert.That(status.CurrentStep, Is.EqualTo(status.FirstSaveStep));
            Assert.That(status.IsStepEnabled(status.FirstSaveStep), Is.True);
        }

        [Test]
        public void FirstSave_RequiresReadyRepositoryWithCommits()
        {
            var status = new SetupStatus { Git = GoodGit, ProjectSettingsOk = true, IgnoreFilesOk = true };
            Assert.That(status.FirstSaveDone, Is.False);

            status.Probe = new RepositoryProbe(RepositoryState.Ready, @"C:\proj");
            Assert.That(status.FirstSaveDone, Is.False, "a repository without commits still needs the first save");

            status.HasCommits = true;
            Assert.That(status.FirstSaveDone, Is.True);
            Assert.That(status.IsComplete, Is.True);
            Assert.That(status.CurrentStep, Is.EqualTo(status.FirstSaveStep + 1));
        }

        [Test]
        public void RootMismatch_IsReportedAndNeverComplete()
        {
            var status = new SetupStatus
            {
                Git = GoodGit, ProjectSettingsOk = true, IgnoreFilesOk = true,
                Probe = new RepositoryProbe(RepositoryState.RootMismatch, @"C:\"), HasCommits = true,
            };
            Assert.That(status.RootMismatch, Is.True);
            Assert.That(status.RepositoryReady, Is.False);
            Assert.That(status.IsComplete, Is.False);
        }

        [Test]
        public void NeedsIdentity_UntilBothFieldsAreKnown()
        {
            var status = new SetupStatus();
            Assert.That(status.NeedsIdentity, Is.True);
            status.Identity = new GitIdentity("Alice", "");
            Assert.That(status.NeedsIdentity, Is.True);
            status.Identity = new GitIdentity("Alice", "alice@example.com");
            Assert.That(status.NeedsIdentity, Is.False);
        }
    }
}
