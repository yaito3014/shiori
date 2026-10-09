using System.Collections.Generic;

namespace Shiori.Editor
{
    /// <summary>
    /// Everything the setup wizard needs to decide which step is next. Plain data so the
    /// gating rules can be unit tested without an editor or a git. Steps are numbered from 1:
    /// git, project settings, ignore files, then one step per extension, then the first save.
    /// </summary>
    internal sealed class SetupStatus
    {
        public const int GitStep = 1;
        public const int ProjectSettingsStep = 2;
        public const int IgnoreFilesStep = 3;

        /// <summary>Number of the first extension step (when there are any).</summary>
        public const int FirstExtensionStep = 4;

        /// <summary>Null until git has been searched for.</summary>
        public GitLocation Git;

        public bool ProjectSettingsOk;

        public bool IgnoreFilesOk;

        /// <summary>Steps contributed by extensions, in display order. Empty when no extension is installed.</summary>
        public List<ExtensionStepStatus> ExtensionSteps = new List<ExtensionStepStatus>();

        /// <summary>Null until the repository has been probed (requires git).</summary>
        public RepositoryProbe Probe;

        public bool HasCommits;

        /// <summary>
        /// The 送信先, or null. The 送信先 step is optional: it never gates <see cref="IsComplete"/>
        /// and is not counted in <see cref="StepCount"/>.
        /// </summary>
        public string RemoteUrl;

        /// <summary>Null until read (requires git).</summary>
        public GitIdentity Identity;

        /// <summary>Number of the 最初の保存 step; moves down as extensions add steps.</summary>
        public int FirstSaveStep => FirstExtensionStep + ExtensionSteps.Count;

        public int StepCount => FirstSaveStep;

        public bool GitOk => Git != null && Git.Found && Git.MeetsMinimumVersion;

        public bool RepositoryReady => Probe != null && Probe.State == RepositoryState.Ready;

        public bool RootMismatch => Probe != null && Probe.State == RepositoryState.RootMismatch;

        public bool NeedsIdentity => Identity == null || !Identity.IsComplete;

        public bool FirstSaveDone => RepositoryReady && HasCommits;

        public bool ExtensionStepsDone
        {
            get
            {
                foreach (var step in ExtensionSteps)
                {
                    if (!step.Done) return false;
                }
                return true;
            }
        }

        /// <summary>The core's own steps. Once setup is finished, only these decide whether the window falls back to the wizard.</summary>
        public bool CoreComplete => GitOk && ProjectSettingsOk && IgnoreFilesOk && FirstSaveDone;

        /// <summary>Everything, extension steps included; the wizard's 完了 needs this.</summary>
        public bool IsComplete => CoreComplete && ExtensionStepsDone;

        /// <summary>The first step that is not finished, or <see cref="StepCount"/> + 1 when everything is done.</summary>
        public int CurrentStep
        {
            get
            {
                for (var step = GitStep; step <= StepCount; step++)
                {
                    if (!IsStepDone(step)) return step;
                }
                return StepCount + 1;
            }
        }

        public bool IsExtensionStep(int step)
        {
            return step >= FirstExtensionStep && step < FirstSaveStep;
        }

        /// <summary>The extension step with this number; only valid when <see cref="IsExtensionStep"/> is true.</summary>
        public ExtensionStepStatus GetExtensionStep(int step)
        {
            return ExtensionSteps[step - FirstExtensionStep];
        }

        public bool IsStepDone(int step)
        {
            switch (step)
            {
                case GitStep: return GitOk;
                case ProjectSettingsStep: return ProjectSettingsOk;
                case IgnoreFilesStep: return IgnoreFilesOk;
            }
            if (IsExtensionStep(step)) return GetExtensionStep(step).Done;
            if (step == FirstSaveStep) return FirstSaveDone;
            return false;
        }

        /// <summary>A step may be acted on only when every earlier step is done.</summary>
        public bool IsStepEnabled(int step)
        {
            for (var i = GitStep; i < step; i++)
            {
                if (!IsStepDone(i)) return false;
            }
            return true;
        }
    }

    /// <summary>One extension step together with its latest evaluation (or the error the evaluation threw).</summary>
    internal sealed class ExtensionStepStatus
    {
        public ShioriExtension Extension { get; }

        /// <summary>Null when the extension failed while creating its steps; <see cref="Error"/> says why.</summary>
        public SetupStep Step { get; }

        /// <summary>Null when <see cref="Error"/> is set.</summary>
        public SetupStepView View { get; set; }

        /// <summary>The exception raised while creating or evaluating the step, or null.</summary>
        public System.Exception Error { get; set; }

        /// <summary>A step that could not be evaluated counts as not done, so the problem stays visible.</summary>
        public bool Done => Error == null && View != null && View.Done;

        /// <summary>The step title, or the package id when the step itself could not be created.</summary>
        public string Title => Step != null && !string.IsNullOrEmpty(Step.Title) ? Step.Title : Extension.PackageId;

        public ExtensionStepStatus(ShioriExtension extension, SetupStep step)
        {
            Extension = extension ?? throw new System.ArgumentNullException(nameof(extension));
            Step = step;
        }
    }
}
