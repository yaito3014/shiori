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

        // Locating git costs two process launches; remember the result for the rest of the domain.
        private static GitLocation _cachedGit;
        private static string _cachedGitForPath;

        /// <summary>
        /// Finds git, reusing the result of an earlier search in this domain unless
        /// <paramref name="refresh"/> is set (the wizard's 再確認) or the configured path changed.
        /// </summary>
        public async Task<GitLocation> LocateGitAsync(CancellationToken cancellationToken, bool refresh = false)
        {
            var configured = User.GitPath ?? string.Empty;
            if (!refresh && _cachedGit != null && _cachedGit.Found && _cachedGitForPath == configured && File.Exists(_cachedGit.Path))
            {
                Git = _cachedGit;
            }
            else
            {
                Git = await new GitLocator(_runner).LocateAsync(configured, cancellationToken);
                _cachedGit = Git;
                _cachedGitForPath = configured;
            }

            Repository = Git.Found && Git.MeetsMinimumVersion
                ? new GitRepository(_runner, Git.Path, ProjectRoot)
                : null;
            return Git;
        }

        /// <summary>Gathers everything the wizard shows. Safe to call when git is missing.</summary>
        public async Task<SetupStatus> EvaluateSetupAsync(CancellationToken cancellationToken, bool refresh = false)
        {
            var status = new SetupStatus
            {
                Git = await LocateGitAsync(cancellationToken, refresh),
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

        /// <summary>
        /// F1 step 4: initialise the repository if needed, set the identity when given, record the
        /// wizard as completed in ProjectSettings/Shiori.json, then add everything and commit.
        /// The settings file is written first so the initial snapshot already contains it;
        /// otherwise it would show up as an uncommitted change right after setup.
        /// </summary>
        public async Task FirstSaveAsync(string name, string email, CancellationToken cancellationToken)
        {
            var repository = Repository ?? throw new InvalidOperationException("git is not available");

            var probe = await repository.ProbeAsync(cancellationToken);
            if (probe.State == RepositoryState.RootMismatch) throw new InvalidOperationException("project is inside another repository: " + probe.TopLevel);
            if (probe.State == RepositoryState.NotARepository) await repository.InitAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
            {
                await repository.SetIdentityAsync(name, email, cancellationToken);
            }

            Project.SetupCompleted = true;
            SaveProjectSettings();

            await repository.AddAllAsync(cancellationToken);
            var staged = await repository.GetStatusAsync(cancellationToken);
            if (staged.HasChanges)
            {
                await repository.CommitAsync(InitialCommitMessage, cancellationToken);
            }
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
