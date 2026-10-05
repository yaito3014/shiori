using System;

namespace Shiori
{
    /// <summary>A git command failed. Carries the exit code and stderr for the UI layer to translate.</summary>
    public class GitException : Exception
    {
        /// <summary>The git arguments that were run, joined with spaces (for diagnostics only).</summary>
        public string Command { get; }

        /// <summary>Process exit code, or -1 when the process could not be started.</summary>
        public int ExitCode { get; }

        public string Stderr { get; }

        public GitException(string command, int exitCode, string stderr, Exception innerException = null)
            : base(BuildMessage(command, exitCode, stderr), innerException)
        {
            Command = command ?? string.Empty;
            ExitCode = exitCode;
            Stderr = stderr ?? string.Empty;
        }

        private static string BuildMessage(string command, int exitCode, string stderr)
        {
            var trimmed = (stderr ?? string.Empty).Trim();
            return trimmed.Length == 0
                ? $"git {command} failed with exit code {exitCode}"
                : $"git {command} failed with exit code {exitCode}: {trimmed}";
        }
    }
}
