using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Shiori.Editor
{
    /// <summary>
    /// Per-project services shared by every view: settings, git location, the repository, and
    /// the installed extensions. Created by the window after each domain reload.
    /// </summary>
    internal sealed class ShioriSession
    {
        public const string InitialCommitMessage = "Initial snapshot (Shiori)";
        public const string GitDownloadUrl = "https://git-scm.com/downloads";

        private readonly IGitRunner _runner;
        private readonly Dictionary<string, ExtensionContext> _contexts = new Dictionary<string, ExtensionContext>(StringComparer.Ordinal);

        public string ProjectRoot { get; }
        public SettingsStore Settings { get; }
        public ShioriProjectSettings Project { get; private set; }
        public ShioriUserSettings User { get; private set; }

        /// <summary>Installed add-ons, sorted by package id. Empty when none is installed.</summary>
        public IReadOnlyList<ShioriExtension> Extensions { get; }

        /// <summary>Null until <see cref="LocateGitAsync"/> has run.</summary>
        public GitLocation Git { get; private set; }

        /// <summary>Null until a usable git has been located.</summary>
        public IGitRepository Repository { get; private set; }

        public string GitIgnorePath => Path.Combine(ProjectRoot, ".gitignore");
        public string GitAttributesPath => Path.Combine(ProjectRoot, ".gitattributes");

        public ShioriSession(string projectRoot) : this(projectRoot, new ProcessGitRunner())
        {
        }

        internal ShioriSession(string projectRoot, IGitRunner runner) : this(projectRoot, runner, ExtensionRegistry.All)
        {
        }

        internal ShioriSession(string projectRoot, IGitRunner runner, IReadOnlyList<ShioriExtension> extensions)
        {
            ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Extensions = extensions ?? Array.Empty<ShioriExtension>();
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

            status.ExtensionSteps.AddRange(await EvaluateExtensionStepsAsync(cancellationToken));

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

        /// <summary>Appends or updates the Shiori blocks; user lines and extension blocks are preserved (F1 step 3).</summary>
        public void WriteIgnoreFiles()
        {
            ManagedBlockWriter.UpsertFile(GitIgnorePath, ShioriBlocks.GitIgnore);
            ManagedBlockWriter.UpsertFile(GitAttributesPath, ShioriBlocks.GitAttributes);
        }

        private static bool BlockMatches(string path, IReadOnlyList<string> expected)
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

        // ---- extensions ----

        /// <summary>The context handed to <paramref name="extension"/>; one per package for the life of the session.</summary>
        public IExtensionContext ContextFor(ShioriExtension extension)
        {
            if (extension == null) throw new ArgumentNullException(nameof(extension));
            if (!_contexts.TryGetValue(extension.PackageId, out var context))
            {
                context = new ExtensionContext(ProjectRoot, L10n.LanguageCode, () => Repository, () => Project, SaveProjectSettings, extension.PackageId);
                _contexts[extension.PackageId] = context;
            }
            return context;
        }

        /// <summary>Creates and evaluates every extension step. A failing extension yields an errored entry instead of throwing.</summary>
        public async Task<List<ExtensionStepStatus>> EvaluateExtensionStepsAsync(CancellationToken cancellationToken)
        {
            var result = new List<ExtensionStepStatus>();
            foreach (var extension in Extensions)
            {
                IReadOnlyList<SetupStep> steps;
                try
                {
                    steps = extension.CreateSetupSteps(ContextFor(extension)) ?? Array.Empty<SetupStep>();
                }
                catch (Exception ex)
                {
                    result.Add(new ExtensionStepStatus(extension, null) { Error = ex });
                    continue;
                }

                foreach (var step in steps)
                {
                    if (step == null) continue;
                    var status = new ExtensionStepStatus(extension, step);
                    await EvaluateExtensionStepAsync(status, cancellationToken);
                    result.Add(status);
                }
            }
            return result;
        }

        /// <summary>Re-runs one step's evaluation, recording the exception instead of throwing.</summary>
        public static async Task EvaluateExtensionStepAsync(ExtensionStepStatus status, CancellationToken cancellationToken)
        {
            if (status.Step == null) return;
            try
            {
                status.View = await status.Step.EvaluateAsync(cancellationToken) ?? new SetupStepView(false, string.Empty);
                status.Error = null;
            }
            catch (Exception ex)
            {
                status.View = null;
                status.Error = ex;
            }
        }

        public async Task RunBeforeSaveAsync(CancellationToken cancellationToken)
        {
            foreach (var extension in Extensions) await extension.BeforeSaveAsync(ContextFor(extension), cancellationToken);
        }

        public async Task RunAfterSaveAsync(string commitHash, CancellationToken cancellationToken)
        {
            foreach (var extension in Extensions) await extension.AfterSaveAsync(ContextFor(extension), commitHash, cancellationToken);
        }

        public async Task RunAfterRestoreAsync(RestoreResult result, CancellationToken cancellationToken)
        {
            foreach (var extension in Extensions) await extension.AfterRestoreAsync(ContextFor(extension), result, cancellationToken);
        }

        /// <summary>The first non-empty memo placeholder, or null. Placeholders do not stack: one line fits the field.</summary>
        public string GetMemoPlaceholder()
        {
            var all = Collect(extension => extension.GetMemoPlaceholder(ContextFor(extension)));
            if (all == null) return null;
            var newline = all.IndexOf('\n');
            return newline < 0 ? all : all.Substring(0, newline);
        }

        /// <summary>All non-empty status chips joined with " / ", or null.</summary>
        public string GetStatusLine()
        {
            return Collect(extension => extension.GetStatusLine(ContextFor(extension)))?.Replace("\n", " / ");
        }

        /// <summary>All non-empty restore warnings for <paramref name="target"/>, one per line, or null.</summary>
        public string GetRestoreWarning(Snapshot target)
        {
            return Collect(extension => extension.GetRestoreWarning(ContextFor(extension), target));
        }

        /// <summary>Decorative text must never break the view: a throwing extension is logged and skipped.</summary>
        private string Collect(Func<ShioriExtension, string> getText)
        {
            StringBuilder sb = null;
            foreach (var extension in Extensions)
            {
                string text;
                try
                {
                    text = getText(extension);
                }
                catch (Exception ex)
                {
                    Debug.LogException(new InvalidOperationException("Shiori extension failed: " + extension.PackageId, ex));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (sb == null) sb = new StringBuilder();
                else sb.Append('\n');
                sb.Append(text.Trim());
            }
            return sb?.ToString();
        }
    }
}
