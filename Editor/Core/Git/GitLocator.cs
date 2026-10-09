using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Shiori
{
    /// <summary>
    /// Finds git in this order: the user-configured path, then PATH, then the default
    /// Git for Windows locations. Never installs anything.
    /// </summary>
    internal sealed class GitLocator
    {
        public static readonly Version MinimumVersion = new Version(2, 30);

        private static readonly Regex VersionPattern = new Regex(@"^git version (\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.Compiled);

        private readonly IGitRunner _runner;

        public GitLocator(IGitRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public async Task<GitLocation> LocateAsync(string configuredPath, CancellationToken cancellationToken)
        {
            var isWindows = IsWindows();
            var candidates = EnumerateCandidates(
                configuredPath,
                Environment.GetEnvironmentVariable("PATH"),
                isWindows,
                DefaultWindowsDirectories());

            var probed = new List<string>();
            // Run version probes from a neutral directory so a broken repository cannot interfere.
            var probeDirectory = Path.GetTempPath();

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                probed.Add(candidate);
                if (!File.Exists(candidate)) continue;

                // Both probes launch a process; run them concurrently since the LFS one is the slow starter.
                var lfsTask = ProbeLfsAsync(candidate, probeDirectory, cancellationToken);
                GitResult result;
                try
                {
                    result = await _runner.RunAsync(candidate, new[] { "--version" }, probeDirectory, cancellationToken).ConfigureAwait(false);
                }
                catch (GitException)
                {
                    await lfsTask.ConfigureAwait(false);
                    continue;
                }

                var versionText = FirstLine(result.Stdout);
                var version = result.Succeeded ? ParseVersion(versionText) : null;
                var lfsText = await lfsTask.ConfigureAwait(false);
                if (version == null) continue;

                return new GitLocation(candidate, version, versionText, lfsText, probed);
            }

            return GitLocation.NotFound(probed);
        }

        /// <summary>LFS is optional in M1; a missing or failing lfs command yields null.</summary>
        private async Task<string> ProbeLfsAsync(string gitPath, string probeDirectory, CancellationToken cancellationToken)
        {
            try
            {
                var lfs = await _runner.RunAsync(gitPath, new[] { "lfs", "version" }, probeDirectory, cancellationToken).ConfigureAwait(false);
                return lfs.Succeeded ? FirstLine(lfs.Stdout) : null;
            }
            catch (GitException)
            {
                return null;
            }
        }

        /// <summary>Parses "git version 2.47.1.windows.1" into 2.47.1. Returns null when unrecognised.</summary>
        internal static Version ParseVersion(string versionLine)
        {
            if (string.IsNullOrEmpty(versionLine)) return null;
            var m = VersionPattern.Match(versionLine.Trim());
            if (!m.Success) return null;
            var major = int.Parse(m.Groups[1].Value);
            var minor = int.Parse(m.Groups[2].Value);
            var patch = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
            return new Version(major, minor, patch);
        }

        /// <summary>
        /// Candidate executables in search order. Duplicates are removed; existence is not checked here.
        /// </summary>
        internal static IReadOnlyList<string> EnumerateCandidates(string configuredPath, string pathEnvironment, bool isWindows, IEnumerable<string> defaultDirectories)
        {
            var seen = new HashSet<string>(isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            var result = new List<string>();
            var exeName = isWindows ? "git.exe" : "git";
            var pathSeparator = isWindows ? ';' : ':';
            var directorySeparator = isWindows ? '\\' : '/';

            string Join(string directory, string name)
            {
                var trimmed = directory.TrimEnd('\\', '/');
                return trimmed.Length == 0 ? directory + name : trimmed + directorySeparator + name;
            }

            void Add(string path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                var trimmed = path.Trim();
                if (seen.Add(trimmed)) result.Add(trimmed);
            }

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var trimmed = configuredPath.Trim();
                // Accept either the executable itself or the directory that contains it.
                if (Directory.Exists(trimmed)) Add(Join(trimmed, exeName));
                else Add(trimmed);
            }

            if (!string.IsNullOrEmpty(pathEnvironment))
            {
                foreach (var dir in pathEnvironment.Split(pathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    Add(Join(dir.Trim().Trim('"'), exeName));
                }
            }

            if (defaultDirectories != null)
            {
                foreach (var dir in defaultDirectories) Add(Join(dir, exeName));
            }

            return result;
        }

        internal static IEnumerable<string> DefaultWindowsDirectories()
        {
            if (!IsWindows()) yield break;

            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (!string.IsNullOrEmpty(programFiles))
            {
                yield return Path.Combine(programFiles, "Git", "cmd");
                yield return Path.Combine(programFiles, "Git", "bin");
            }
            else
            {
                yield return @"C:\Program Files\Git\cmd";
                yield return @"C:\Program Files\Git\bin";
            }

            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData))
            {
                yield return Path.Combine(localAppData, "Programs", "Git", "cmd");
            }
        }

        private static bool IsWindows()
        {
            return Path.DirectorySeparatorChar == '\\';
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var idx = text.IndexOfAny(new[] { '\r', '\n' });
            return (idx < 0 ? text : text.Substring(0, idx)).Trim();
        }
    }
}
