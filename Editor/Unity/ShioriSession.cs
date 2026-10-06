using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Shiori.Editor
{
    /// <summary>
    /// Per-project services shared by every view: settings, git location, and the repository.
    /// Created by the window after each domain reload.
    /// </summary>
    internal sealed class ShioriSession
    {
        public const string InitialCommitMessage = "Initial snapshot (Shiori)";
        public const string GitDownloadUrl = "https://git-scm.com/downloads";

        private readonly IGitRunner _runner;

        public string ProjectRoot { get; }
        public SettingsStore Settings { get; }
        public ShioriProjectSettings Project { get; private set; }
        public ShioriUserSettings User { get; private set; }

        /// <summary>Null until <see cref="LocateGitAsync"/> has run.</summary>
        public GitLocation Git { get; private set; }

        /// <summary>Null until a usable git has been located.</summary>
        public IGitRepository Repository { get; private set; }

        public string GitIgnorePath => Path.Combine(ProjectRoot, ".gitignore");
        public string GitAttributesPath => Path.Combine(ProjectRoot, ".gitattributes");

        public ShioriSession(string projectRoot) : this(projectRoot, new ProcessGitRunner())
        {
        }

        internal ShioriSession(string projectRoot, IGitRunner runner)
        {
            ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Settings = new SettingsStore(projectRoot);
            ReloadSettings();
        }

        /// <summary>The Unity project root: the parent of Assets/.</summary>
        public static string DetectProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        /// <summary>Re-reads both settings files. Throws <see cref="SettingsFormatException"/> on corrupt files.</summary>
        public void ReloadSettings()
        {
            Project = Settings.LoadProject();
            User = Settings.LoadUser();
        }

        public void SaveProjectSettings()
        {
            Settings.SaveProject(Project);
        }

        public void SaveUserSettings()
        {
            Settings.SaveUser(User);
        }

        public async Task<GitLocation> LocateGitAsync(CancellationToken cancellationToken)
        {
            Git = await new GitLocator(_runner).LocateAsync(User.GitPath, cancellationToken);
            Repository = Git.Found && Git.MeetsMinimumVersion
                ? new GitRepository(_runner, Git.Path, ProjectRoot)
                : null;
            return Git;
        }

        /// <summary>Gathers everything the wizard shows. Safe to call when git is missing.</summary>
        public async Task<SetupStatus> EvaluateSetupAsync(CancellationToken cancellationToken)
        {
            var status = new SetupStatus
            {
                Git = await LocateGitAsync(cancellationToken),
                ProjectSettingsOk = UnityProjectSettings.IsConfigured,
                IgnoreFilesOk = AreIgnoreFilesWritten(),
            };

            if (Repository != null)
            {
                status.Probe = await Repository.ProbeAsync(cancellationToken);
                if (status.Probe.State == RepositoryState.Ready)
                {
                    status.HasCommits = await Repository.GetHeadAsync(cancellationToken) != null;
                }
                status.Identity = await Repository.GetIdentityAsync(cancellationToken);
            }

            return status;
        }

        public bool AreIgnoreFilesWritten()
        {
            return BlockMatches(GitIgnorePath, ShioriBlocks.GitIgnore) && BlockMatches(GitAttributesPath, ShioriBlocks.GitAttributes);
        }

        /// <summary>Appends or updates the Shiori blocks; user lines are preserved (F1 step 3).</summary>
        public void WriteIgnoreFiles()
        {
            ManagedBlockWriter.UpsertFile(GitIgnorePath, ShioriBlocks.GitIgnore);
            ManagedBlockWriter.UpsertFile(GitAttributesPath, ShioriBlocks.GitAttributes);
        }

        private static bool BlockMatches(string path, System.Collections.Generic.IReadOnlyList<string> expected)
        {
            if (!File.Exists(path)) return false;
            var actual = ManagedBlockWriter.ReadBlock(File.ReadAllText(path));
            if (actual == null || actual.Count != expected.Count) return false;
            for (var i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}
