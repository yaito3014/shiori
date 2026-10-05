using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shiori
{
    /// <summary>
    /// Maintains one marker-delimited block inside a user-owned text file such as
    /// <c>.gitignore</c>. Everything outside the markers is preserved byte for byte,
    /// including the file's line-ending style.
    /// </summary>
    internal static class ManagedBlockWriter
    {
        public const string StartMarker = "# >>> shiori (managed block, do not edit) >>>";
        public const string EndMarker = "# <<< shiori <<<";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Writes or updates the managed block in <paramref name="filePath"/>, creating the file when absent.
        /// Returns true when the file content changed.
        /// </summary>
        public static bool UpsertFile(string filePath, IReadOnlyList<string> blockLines)
        {
            var existing = File.Exists(filePath) ? File.ReadAllText(filePath, Utf8NoBom) : null;
            var updated = Upsert(existing, blockLines);
            if (existing == updated) return false;

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, updated, Utf8NoBom);
            return true;
        }

        /// <summary>
        /// Pure form of <see cref="UpsertFile"/>: <paramref name="existingContent"/> is null when the file does not exist.
        /// </summary>
        public static string Upsert(string existingContent, IReadOnlyList<string> blockLines)
        {
            if (blockLines == null) throw new ArgumentNullException(nameof(blockLines));

            var newline = DetectNewline(existingContent);
            var block = BuildBlock(blockLines, newline);

            if (string.IsNullOrEmpty(existingContent))
            {
                return block + newline;
            }

            var start = FindLineStart(existingContent, StartMarker);
            if (start >= 0)
            {
                var endMarkerIndex = FindLineStart(existingContent, EndMarker, start);
                if (endMarkerIndex >= 0)
                {
                    var endOfEndMarker = endMarkerIndex + EndMarker.Length;
                    return existingContent.Substring(0, start) + block + existingContent.Substring(endOfEndMarker);
                }
                // Start marker without an end marker: treat the rest of the file as user content and append a fresh block.
            }

            var sb = new StringBuilder(existingContent);
            if (!existingContent.EndsWith("\n")) sb.Append(newline);
            // Keep one blank line between user content and the block for readability.
            if (!existingContent.EndsWith("\n\n") && !existingContent.EndsWith("\r\n\r\n")) sb.Append(newline);
            sb.Append(block);
            sb.Append(newline);
            return sb.ToString();
        }

        /// <summary>Returns the lines currently inside the managed block, or null when there is no block.</summary>
        public static IReadOnlyList<string> ReadBlock(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var start = FindLineStart(content, StartMarker);
            if (start < 0) return null;
            var end = FindLineStart(content, EndMarker, start);
            if (end < 0) return null;

            var inner = content.Substring(start + StartMarker.Length, end - (start + StartMarker.Length));
            var lines = inner.Replace("\r\n", "\n").Split('\n');
            var result = new List<string>();
            // The inner text starts and ends with the newline that surrounds the markers.
            for (var i = 1; i < lines.Length - 1; i++) result.Add(lines[i]);
            return result;
        }

        internal static string DetectNewline(string content)
        {
            if (string.IsNullOrEmpty(content)) return "\n";
            var lf = content.IndexOf('\n');
            if (lf < 0) return "\n";
            return lf > 0 && content[lf - 1] == '\r' ? "\r\n" : "\n";
        }

        private static string BuildBlock(IReadOnlyList<string> lines, string newline)
        {
            var sb = new StringBuilder();
            sb.Append(StartMarker);
            foreach (var line in lines)
            {
                sb.Append(newline);
                sb.Append(line ?? string.Empty);
            }
            sb.Append(newline);
            sb.Append(EndMarker);
            return sb.ToString();
        }

        /// <summary>Finds <paramref name="marker"/> occurring as a whole line (ignoring trailing CR).</summary>
        private static int FindLineStart(string content, string marker, int from = 0)
        {
            var index = content.IndexOf(marker, from, StringComparison.Ordinal);
            while (index >= 0)
            {
                var atLineStart = index == 0 || content[index - 1] == '\n';
                var after = index + marker.Length;
                var atLineEnd = after == content.Length || content[after] == '\n' || content[after] == '\r';
                if (atLineStart && atLineEnd) return index;
                index = content.IndexOf(marker, after, StringComparison.Ordinal);
            }
            return -1;
        }
    }
}
