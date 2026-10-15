using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    public enum LargeFileState
    {
        /// <summary>Git LFS is installed and set up in this clone, and Shiori's block (if Shiori wrote one) is current.</summary>
        Ready,

        /// <summary>The project does not use Git LFS yet; the step offers to set it up or to go without.</summary>
        NotSetUp,

        /// <summary>The project chose to go without Git LFS (and does not use it).</summary>
        Skipped,

        /// <summary>Shiori's <c>shiori-lfs</c> block lists other patterns than this version's.</summary>
        Outdated,

        /// <summary>Git LFS is installed, but this clone lacks its filter or its pre-push hook.</summary>
        NeedsInstall,

        /// <summary>The git-lfs command is not installed on this PC.</summary>
        ToolMissing,

        /// <summary>The project is inside another repository; the first-save step explains it.</summary>
        RootMismatch,
    }

    /// <summary>The result of <see cref="LargeFileSetup.EvaluateAsync"/>.</summary>
    public sealed class LargeFileStatus
    {
        public LargeFileState State { get; }

        /// <summary>The project's .gitattributes sends some files through Git LFS (Shiori's block or the user's own lines).</summary>
        public bool UsedByProject { get; }

        /// <summary>
        /// The project uses Git LFS but this PC cannot run it. Taking files out of history (受信, 戻す, switching a
        /// variation, ...) would then write LFS pointer text in place of textures and models, so those wait.
        /// </summary>
        public bool Blocked => UsedByProject && (State == LargeFileState.ToolMissing || State == LargeFileState.NeedsInstall);

        /// <summary>Shiori's <c>shiori-lfs</c> block is in .gitattributes, so Shiori can take it out again.</summary>
        public bool HasBlock { get; }

        public LargeFileStatus(LargeFileState state, bool usedByProject, bool hasBlock = false)
        {
            State = state;
            UsedByProject = usedByProject;
            HasBlock = hasBlock;
        }
    }

    /// <summary>
    /// Git LFS for images, models, audio and video: Shiori's <c>shiori-lfs</c> block in .gitattributes, and
    /// <c>git lfs install --local</c> (filter and hooks) in this clone. Only new saves go to LFS; history is never rewritten.
    /// </summary>
    internal static class LargeFileSetup
    {
        public const string BlockId = "shiori-lfs";
        public const string DownloadUrl = "https://git-lfs.com";

        private const string Attributes = " filter=lfs diff=lfs merge=lfs -text";

        /// <summary>The file types sent through LFS. Binary formats only; Unity's text assets stay in git for merging.</summary>
        public static readonly IReadOnlyList<string> Extensions = new[]
        {
            // images
            "png", "jpg", "jpeg", "tga", "tif", "tiff", "psd", "psb", "exr", "hdr", "bmp", "gif", "webp",
            // models
            "fbx", "obj", "blend", "dae", "3ds", "max", "ma", "mb",
            // audio
            "wav", "mp3", "ogg", "aif", "aiff", "flac",
            // video
            "mp4", "mov", "webm", "avi",
            // fonts and archives
            "ttf", "otf", "zip", "7z", "unitypackage",
        };

        public static readonly IReadOnlyList<string> GitAttributes = BuildAttributes();

        private static IReadOnlyList<string> BuildAttributes()
        {
            var lines = new List<string>();
            foreach (var extension in Extensions)
            {
                lines.Add("*." + extension + Attributes);
                // Attribute patterns are case-sensitive; cameras and tools often write upper-case extensions.
                lines.Add("*." + extension.ToUpperInvariant() + Attributes);
            }
            return lines;
        }

        /// <summary>The state of this project and PC. <paramref name="skipped"/> is the project's "go without" choice.</summary>
        public static async Task<LargeFileStatus> EvaluateAsync(IGitRepository repository, bool skipped, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));

            var probe = await repository.ProbeAsync(cancellationToken).ConfigureAwait(false);
            if (probe.State == RepositoryState.RootMismatch) return new LargeFileStatus(LargeFileState.RootMismatch, false);

            var text = ReadAttributes(repository.WorkingDirectory);
            var used = UsesLfs(text);
            if (!used && skipped) return new LargeFileStatus(LargeFileState.Skipped, false);
            if (!await repository.HasLfsAsync(cancellationToken).ConfigureAwait(false)) return new LargeFileStatus(LargeFileState.ToolMissing, used);

            var installed = probe.State == RepositoryState.Ready && await IsInstalledAsync(repository, cancellationToken).ConfigureAwait(false);
            if (used && !installed) return new LargeFileStatus(LargeFileState.NeedsInstall, true);

            var block = text == null ? null : ManagedBlockWriter.ReadBlock(text, BlockId);
            if (block == null) return new LargeFileStatus(used ? LargeFileState.Ready : LargeFileState.NotSetUp, used);
            if (!SameLines(block, GitAttributes)) return new LargeFileStatus(LargeFileState.Outdated, used, true);
            return new LargeFileStatus(LargeFileState.Ready, used, true);
        }

        /// <summary>
        /// Sets Git LFS up in this clone and writes the block. Creates the repository when there is none yet (the wizard
        /// runs this step before the first save). Files already in history stay as they are; each goes to LFS the next
        /// time it is saved.
        /// </summary>
        public static async Task ApplyAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            var probe = await repository.ProbeAsync(cancellationToken).ConfigureAwait(false);
            if (probe.State == RepositoryState.RootMismatch) throw new InvalidOperationException("project is inside another repository: " + probe.TopLevel);
            if (probe.State == RepositoryState.NotARepository) await repository.InitAsync(cancellationToken).ConfigureAwait(false);

            await repository.InstallLfsAsync(cancellationToken).ConfigureAwait(false);
            ManagedBlockWriter.UpsertFile(Path.Combine(repository.WorkingDirectory, ".gitattributes"), BlockId, GitAttributes);
        }

        /// <summary>
        /// For a clone that has LFS files but was made without Git LFS: installs the filter and hooks, then downloads the
        /// files whose working copy is still a pointer (<c>git lfs pull</c>). Rewrites those files, so the caller holds
        /// the AssetDatabase around it.
        /// </summary>
        public static async Task InstallAndPullAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            await repository.InstallLfsAsync(cancellationToken).ConfigureAwait(false);
            if (await repository.GetHeadAsync(cancellationToken).ConfigureAwait(false) == null) return;
            // Without a 送信先 there is nowhere to download from.
            if (await repository.GetRemoteUrlAsync(cancellationToken).ConfigureAwait(false) == null) return;
            await repository.PullLfsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops using Git LFS for new saves: removes Shiori's block, then stages <paramref name="lfsFiles"/> (from
        /// <see cref="IGitRepository.ListLfsFilesAsync"/>, taken before) again as ordinary files, so the next save
        /// records them in git. Without that, the current version would keep LFS pointers that no attribute smudges any
        /// more, and taking it out later would write pointer text. Only the index changes; files the user's own lines
        /// still send through LFS stay in LFS. Earlier saves are not rewritten.
        /// </summary>
        public static async Task DisableAsync(IGitRepository repository, IReadOnlyList<string> lfsFiles, CancellationToken cancellationToken)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            ManagedBlockWriter.RemoveFile(Path.Combine(repository.WorkingDirectory, ".gitattributes"), BlockId);
            var present = new List<string>();
            foreach (var path in lfsFiles ?? Array.Empty<string>())
            {
                // Deleted files have nothing to stage again; the next save records the deletion.
                if (File.Exists(Path.Combine(repository.WorkingDirectory, path))) present.Add(path);
            }
            await repository.RenormalizeAsync(present, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>True when a non-comment line of .gitattributes sends files through the lfs filter.</summary>
        public static bool UsesLfs(string gitAttributes)
        {
            if (string.IsNullOrEmpty(gitAttributes)) return false;
            foreach (var raw in gitAttributes.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                foreach (var part in line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part == "filter=lfs") return true;
                }
            }
            return false;
        }

        /// <summary>The lfs filter is configured (any scope; Git for Windows sets it system-wide) and the pre-push hook that uploads LFS files exists.</summary>
        private static async Task<bool> IsInstalledAsync(IGitRepository repository, CancellationToken cancellationToken)
        {
            var filter = await repository.GetConfigAsync("filter.lfs.process", cancellationToken).ConfigureAwait(false)
                         ?? await repository.GetConfigAsync("filter.lfs.smudge", cancellationToken).ConfigureAwait(false);
            if (filter == null) return false;
            var hook = await repository.GetGitPathAsync("hooks/pre-push", cancellationToken).ConfigureAwait(false);
            if (hook == null || !File.Exists(hook)) return false;
            var script = File.ReadAllText(hook);
            return script.Contains("git lfs") || script.Contains("git-lfs");
        }

        private static string ReadAttributes(string root)
        {
            var path = Path.Combine(root, ".gitattributes");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static bool SameLines(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
        {
            if (actual.Count != expected.Count) return false;
            for (var i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}
