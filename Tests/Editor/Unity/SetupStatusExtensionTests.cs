using System;
using NUnit.Framework;

namespace Shiori.Editor.Tests
{
    /// <summary>Gating with extension steps inserted between 履歴に含めないもの and 最初の保存.</summary>
    public class SetupStatusExtensionTests
    {
        private static GitLocation GoodGit => new GitLocation(@"C:\git\git.exe", new Version(2, 47, 1), "git version 2.47.1", null, null);

        private static ExtensionStepStatus Step(bool done)
        {
            return new ExtensionStepStatus(new FakeExtension(), null) { View = new SetupStepView(done, "m") };
        }

        [Test]
        public void NoExtensions_KeepsTheFourCoreSteps()
        {
            var status = new SetupStatus();
            Assert.That(status.StepCount, Is.EqualTo(4));
            Assert.That(status.FirstSaveStep, Is.EqualTo(4));
            Assert.That(status.IsExtensionStep(4), Is.False);
            Assert.That(status.ExtensionStepsDone, Is.True);
        }

        [Test]
        public void ExtensionSteps_PushFirstSaveDownAndGateIt()
        {
            var status = new SetupStatus { Git = GoodGit, ProjectSettingsOk = true, IgnoreFilesOk = true };
            status.ExtensionSteps.Add(Step(false));
            status.ExtensionSteps.Add(Step(true));

            Assert.That(status.StepCount, Is.EqualTo(6));
            Assert.That(status.FirstSaveStep, Is.EqualTo(6));
            Assert.That(status.IsExtensionStep(4), Is.True);
            Assert.That(status.IsExtensionStep(5), Is.True);
            Assert.That(status.IsExtensionStep(6), Is.False);
            Assert.That(status.CurrentStep, Is.EqualTo(4));
            Assert.That(status.IsStepDone(4), Is.False);
            Assert.That(status.IsStepDone(5), Is.True);
            Assert.That(status.IsStepEnabled(5), Is.False, "step 5 waits for step 4");
            Assert.That(status.IsStepEnabled(6), Is.False, "first save waits for every extension step");

            status.ExtensionSteps[0].View = new SetupStepView(true, "ok");
            Assert.That(status.CurrentStep, Is.EqualTo(6));
            Assert.That(status.IsStepEnabled(6), Is.True);

            status.Probe = new RepositoryProbe(RepositoryState.Ready, @"C:\proj");
            status.HasCommits = true;
            Assert.That(status.IsComplete, Is.True);
            Assert.That(status.CurrentStep, Is.EqualTo(7));
        }

        [Test]
        public void PendingExtensionStep_DoesNotUndoCoreCompletion()
        {
            var status = new SetupStatus
            {
                Git = GoodGit, ProjectSettingsOk = true, IgnoreFilesOk = true,
                Probe = new RepositoryProbe(RepositoryState.Ready, @"C:\proj"), HasCommits = true,
            };
            status.ExtensionSteps.Add(Step(false));
            Assert.That(status.CoreComplete, Is.True, "the window keeps showing the main view");
            Assert.That(status.IsComplete, Is.False, "but the wizard's 完了 waits for the extension step");
        }

        [Test]
        public void ErroredStep_CountsAsNotDoneAndUsesPackageIdAsTitle()
        {
            var errored = new ExtensionStepStatus(new FakeExtension(), null) { Error = new InvalidOperationException("boom") };
            Assert.That(errored.Done, Is.False);
            Assert.That(errored.Title, Is.EqualTo(FakeExtension.Id));

            var status = new SetupStatus { Git = GoodGit, ProjectSettingsOk = true, IgnoreFilesOk = true };
            status.ExtensionSteps.Add(errored);
            Assert.That(status.IsComplete, Is.False);
            Assert.That(status.CurrentStep, Is.EqualTo(4));
        }
    }
}
