using System.Collections.Generic;
using System.Text;

namespace Shiori
{
    /// <summary>
    /// Builds a single argument string from individual arguments using the quoting rules
    /// understood by CommandLineToArgvW / the MS C runtime (which git for Windows uses)
    /// and by Mono's argument parser on other platforms.
    /// </summary>
    internal static class CommandLine
    {
        public static string Join(IReadOnlyList<string> args)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < args.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                AppendQuoted(sb, args[i] ?? string.Empty);
            }
            return sb.ToString();
        }

        public static string Quote(string arg)
        {
            var sb = new StringBuilder();
            AppendQuoted(sb, arg ?? string.Empty);
            return sb.ToString();
        }

        private static void AppendQuoted(StringBuilder sb, string arg)
        {
            if (arg.Length > 0 && !NeedsQuoting(arg))
            {
                sb.Append(arg);
                return;
            }

            sb.Append('"');
            var backslashes = 0;
            foreach (var c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    // Backslashes before a quote must be doubled, then the quote escaped.
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                    continue;
                }

                sb.Append('\\', backslashes);
                backslashes = 0;
                sb.Append(c);
            }

            // Trailing backslashes must be doubled so the closing quote is not escaped.
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
        }

        private static bool NeedsQuoting(string arg)
        {
            foreach (var c in arg)
            {
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '"' || c == '\v') return true;
            }
            return false;
        }
    }
}
