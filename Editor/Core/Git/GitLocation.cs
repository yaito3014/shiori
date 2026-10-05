using System;
using System.Collections.Generic;

namespace Shiori
{
    /// <summary>Result of searching for a usable git executable.</summary>
    public sealed class GitLocation
    {
        /// <summary>Full path of the executable, or null when none was found.</summary>
        public string Path { get; }

        /// <summary>Parsed version (major.minor.patch), or null when not found.</summary>
        public Version Version { get; }

        /// <summary>The raw first line of <c>git --version</c>, or null.</summary>
        public string VersionText { get; }

        /// <summary>The raw first line of <c>git lfs version</c>, or null when LFS is unavailable.</summary>
        public string LfsVersionText { get; }

        /// <summary>Every path that was tried, in order, for diagnostics.</summary>
        public IReadOnlyList<string> Probed { get; }

        public bool Found => Path != null;

        public bool HasLfs => LfsVersionText != null;

        public bool MeetsMinimumVersion => Version != null && Version >= GitLocator.MinimumVersion;

        public GitLocation(string path, Version version, string versionText, string lfsVersionText, IReadOnlyList<string> probed)
        {
            Path = path;
            Version = version;
            VersionText = versionText;
            LfsVersionText = lfsVersionText;
            Probed = probed ?? Array.Empty<string>();
        }

        public static GitLocation NotFound(IReadOnlyList<string> probed)
        {
            return new GitLocation(null, null, null, null, probed);
        }
    }
}
