using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    public enum MergeDriverState
    {
        /// <summary>The driver is in .git/config with this editor's tool, and the .gitattributes block is in place.</summary>
        Ready,

        /// <summary>Nothing is set up yet.</summary>
        Missing,

        /// <summary>Set up, but for another editor (the Unity version changed) or with an older block.</summary>
        Outdated,

        /// <summary>This editor has no UnityYAMLMerge; there is nothing to set up.</summary>
        ToolMissing,

        /// <summary>The project is inside another repository; the first-save step explains it.</summary>
        RootMismatch,

        /// <summary>The project chose to merge scenes and prefabs with git's own line merge.</summary>
        Skipped,
    }

    /// <summary>
    /// UnityYAMLMerge as git's merge driver (M5): <c>merge.unityyamlmerge.*</c> in the repository's own .git/config,
    /// pointing at the running editor's tool, and Shiori's <c>shiori-merge</c> block in .gitattributes naming the
    /// Unity YAML file types. Git then hands those files to the tool whenever both sides changed them.
    /// </summary>
    internal static class MergeDriverSetup
    {
        public const string DriverName = "unityyamlmerge";
        public const string BlockId = "shiori-merge";
        public const string NameKey = "merge." + DriverName + ".name";
        public const string DriverKey = "merge." + DriverName + ".driver";
        public const string DriverDescription = "Unity YAML (Shiori)";

        /// <summary>
        /// Unity's text-serialized asset types. The core block marks .unity / .prefab / .asset already; this block
        /// covers the rest so it works on its own.
        /// </summary>
        public static readonly IReadOnlyList<string> GitAttributes = new[]
        {
            "*.unity merge=" + DriverName,
            "*.prefab merge=" + DriverName,
            "*.asset merge=" + DriverName,
            "*.mat merge=" + DriverName,
            "*.anim merge=" + DriverName,
            "*.controller merge=" + DriverName,
            "*.overrideController merge=" + DriverName,
            "*.mask merge=" + DriverName,
            "*.playable merge=" + DriverName,
            "*.physicMaterial merge=" + DriverName,
            "*.physicsMaterial2D merge=" + DriverName,
            "*.lighting merge=" + DriverName,
            "*.mixer merge=" + DriverName,
            "*.preset merge=" + DriverName,
            "*.spriteatlas merge=" + DriverName,
            "*.terrainlayer merge=" + DriverName,
            "*.renderTexture merge=" + DriverName,
        };

        /// <summary>
        /// The driver command. Git names its temporary files without an extension, so <c>--force</c> is needed for the
        /// tool to merge them at all; <c>-h</c> and <c>--fallback none</c> keep it from opening any window. What it cannot
        /// combine makes it fail, and git reports the file as conflicted.
        /// </summary>
        public static string DriverCommand(string toolPath)
        {
            if (string.IsNullOrWhiteSpace(toolPath)) throw new ArgumentException("tool path is required", nameof(toolPath));
            return "\"" + toolPath.Replace('\\', '/') + "\" merge -h --force --fallback none %O %B %A %A";
        }

        /// <summary>
        /// The state for this PC. <paramref name="skipped"/> is the project's "go without" choice; it counts only while
        /// the project's .gitattributes has no <c>shiori-merge</c> block (a block another PC wrote wins).
        /// </summary>
        public static async Task<MergeDriverState> EvaluateAsync(IGitRepository repository, string toolPath, CancellationToken cancellationToken, bool skipped = false)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (skipped && ReadBlock(repository.WorkingDirectory) == null) return MergeDriverState.Skipped;
            if (string.IsNullOrEmpty(toolPath) || !File.Exists(toolPath)) return MergeDriverState.ToolMissing;

            var probe = await repository.ProbeAsync(cancellationToken).ConfigureAwait(false);
            if (probe.State == RepositoryState.RootMismatch) return MergeDriverState.RootMismatch;
            if (probe.State == RepositoryState.NotARepository) return MergeDriverState.Missing;

            var driver = await repository.GetLocalConfigAsync(DriverKey, cancellationToken).ConfigureAwait(false);
            var block = ReadBlock(repository.WorkingDirectory);
            if (driver == null && block == null) return MergeDriverState.Missing;
            if (!string.Equals(driver, DriverCommand(toolPath), StringComparison.Ordinal) || !SameLines(block, GitAttributes)) return MergeDriverState.Outdated;
            return MergeDriverState.Ready;
        }

        /// <summary>
        /// Writes the driver and the block. Creates the repository when there is none yet (the wizard runs this step
        /// before the first save); the first save then records the block.
        /// </summary>
        public static async Task ApplyAsync(IGitRepository repository, string toolPath, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (string.IsNullOrEmpty(toolPath) || !File.Exists(toolPath)) throw new FileNotFoundException("UnityYAMLMerge was not found", toolPath);

            var probe = await repository.ProbeAsync(cancellationToken).ConfigureAwait(false);
            if (probe.State == RepositoryState.RootMismatch) throw new InvalidOperationException("project is inside another repository: " + probe.TopLevel);
            if (probe.State == RepositoryState.NotARepository) await repository.InitAsync(cancellationToken).ConfigureAwait(false);

            await repository.SetLocalConfigAsync(NameKey, DriverDescription, cancellationToken).ConfigureAwait(false);
            await repository.SetLocalConfigAsync(DriverKey, DriverCommand(toolPath), cancellationToken).ConfigureAwait(false);
            ManagedBlockWriter.UpsertFile(Path.Combine(repository.WorkingDirectory, ".gitattributes"), BlockId, GitAttributes);
        }

        /// <summary>
        /// Goes back to git's own line merge for scenes and prefabs: removes the driver from .git/config and the block
        /// from .gitattributes. The core block still names the driver for .unity / .prefab / .asset; git falls back to
        /// its text merge for a driver that is not defined.
        /// </summary>
        public static async Task DisableAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            await repository.UnsetLocalConfigAsync(DriverKey, cancellationToken).ConfigureAwait(false);
            await repository.UnsetLocalConfigAsync(NameKey, cancellationToken).ConfigureAwait(false);
            ManagedBlockWriter.RemoveFile(Path.Combine(repository.WorkingDirectory, ".gitattributes"), BlockId);
        }

        private static IReadOnlyList<string> ReadBlock(string root)
        {
            var path = Path.Combine(root, ".gitattributes");
            return File.Exists(path) ? ManagedBlockWriter.ReadBlock(File.ReadAllText(path), BlockId) : null;
        }

        private static bool SameLines(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
        {
            if (actual == null || actual.Count != expected.Count) return false;
            for (var i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}
