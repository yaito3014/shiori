namespace Shiori.Editor
{
    /// <summary>
    /// Everything the setup wizard needs to decide which step is next. Plain data so the
    /// gating rules can be unit tested without an editor or a git.
    /// </summary>
    internal sealed class SetupStatus
    {
        public const int GitStep = 1;
        public const int ProjectSettingsStep = 2;
        public const int IgnoreFilesStep = 3;
        public const int FirstSaveStep = 4;

        /// <summary>Null until git has been searched for.</summary>
        public GitLocation Git;

        public bool ProjectSettingsOk;

        public bool IgnoreFilesOk;

        /// <summary>Null until the repository has been probed (requires git).</summary>
        public RepositoryProbe Probe;

        public bool HasCommits;

        /// <summary>Null until read (requires git).</summary>
        public GitIdentity Identity;

        public bool GitOk => Git != null && Git.Found && Git.MeetsMinimumVersion;

        public bool RepositoryReady => Probe != null && Probe.State == RepositoryState.Ready;

        public bool RootMismatch => Probe != null && Probe.State == RepositoryState.RootMismatch;

        public bool NeedsIdentity => Identity == null || !Identity.IsComplete;

        public bool FirstSaveDone => RepositoryReady && HasCommits;

        public bool IsComplete => GitOk && ProjectSettingsOk && IgnoreFilesOk && FirstSaveDone;

        /// <summary>The first step that is not finished, or 5 when everything is done.</summary>
        public int CurrentStep
        {
            get
            {
                if (!GitOk) return GitStep;
                if (!ProjectSettingsOk) return ProjectSettingsStep;
                if (!IgnoreFilesOk) return IgnoreFilesStep;
                if (!FirstSaveDone) return FirstSaveStep;
                return FirstSaveStep + 1;
            }
        }

        public bool IsStepDone(int step)
        {
            switch (step)
            {
                case GitStep: return GitOk;
                case ProjectSettingsStep: return ProjectSettingsOk;
                case IgnoreFilesStep: return IgnoreFilesOk;
                case FirstSaveStep: return FirstSaveDone;
                default: return false;
            }
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
}
