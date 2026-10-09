using System;

namespace Shiori
{
    /// <summary>Why talking to the 送信先 failed, in terms the UI can explain without git words.</summary>
    public enum RemoteErrorKind
    {
        Unknown,

        /// <summary>Sign-in failed or was cancelled, or the account may not write there.</summary>
        Authentication,

        /// <summary>The URL does not lead to a repository.</summary>
        NotFound,

        /// <summary>No connection: offline, DNS, proxy, TLS.</summary>
        Network,

        /// <summary>The 送信先 has saves this PC does not have (non-fast-forward).</summary>
        Rejected,

        /// <summary>A file is larger than the host accepts (for example GitHub's 100 MB limit).</summary>
        TooLarge,

        /// <summary>The operation took longer than Shiori waits.</summary>
        Timeout,
    }

    /// <summary>A remote operation failed; <see cref="Kind"/> says why. The git output stays in <see cref="GitException.Stderr"/>.</summary>
    public sealed class RemoteOperationException : GitException
    {
        public RemoteErrorKind Kind { get; }

        public RemoteOperationException(RemoteErrorKind kind, string command, int exitCode, string output, Exception innerException = null)
            : base(command, exitCode, output, innerException)
        {
            Kind = kind;
        }
    }

    /// <summary>
    /// Sorts git's (English, LC_ALL=C) remote error output into <see cref="RemoteErrorKind"/>. The patterns
    /// cover git itself and the common hosts; anything else is <see cref="RemoteErrorKind.Unknown"/> and the
    /// raw output is shown.
    /// </summary>
    public static class RemoteErrorClassifier
    {
        public static RemoteErrorKind Classify(string output)
        {
            var text = output ?? string.Empty;
            // Order matters: a size rejection also says "rejected", and auth failures can mention the URL.
            if (Contains(text, "GH001") || Contains(text, "exceeds GitHub's file size limit") || Contains(text, "file size limit")
                || Contains(text, "this exceeds") || Contains(text, "too large")) return RemoteErrorKind.TooLarge;
            if (Contains(text, "[rejected]") || Contains(text, "non-fast-forward") || Contains(text, "fetch first")
                || Contains(text, "Updates were rejected")) return RemoteErrorKind.Rejected;
            if (Contains(text, "Authentication failed") || Contains(text, "could not read Username") || Contains(text, "could not read Password")
                || Contains(text, "Permission denied") || HasStatus(text, "403") || HasStatus(text, "401")
                || Contains(text, "terminal prompts disabled") || Contains(text, "Invalid username or password")) return RemoteErrorKind.Authentication;
            if (Contains(text, "Repository not found") || Contains(text, "does not appear to be a git repository")
                || Contains(text, "not found") || HasStatus(text, "404")) return RemoteErrorKind.NotFound;
            if (Contains(text, "Could not resolve host") || Contains(text, "Failed to connect") || Contains(text, "Connection timed out")
                || Contains(text, "Connection refused") || Contains(text, "unable to access") || Contains(text, "Network is unreachable")
                || Contains(text, "SSL") || Contains(text, "proxy")) return RemoteErrorKind.Network;
            return RemoteErrorKind.Unknown;
        }

        private static bool Contains(string text, string fragment)
        {
            return text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>An HTTP status as a whole number, so commit hashes such as "a403f…" do not match.</summary>
        private static bool HasStatus(string text, string code)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<![0-9A-Za-z])" + code + @"(?![0-9A-Za-z])");
        }
    }
}
