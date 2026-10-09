using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shiori
{
    /// <summary>
    /// Maintains marker-delimited blocks inside a user-owned text file such as <c>.gitignore</c>.
    /// Each block has an id (<c>shiori</c> for the core, the package name for add-ons) so several
    /// packages can own one block each in the same file. Everything outside a block is preserved
    /// byte for byte, including the file's line-ending style.
    /// </summary>
    internal static class ManagedBlockWriter
    {
        public const string DefaultBlockId = "shiori";

        /// <summary>Markers of the core block, kept as constants because older files were written with them.</summary>
        public const string StartMarker = "# >>> shiori (managed block, do not edit) >>>";
        public const string EndMarker = "# <<< shiori <<<";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string StartMarkerFor(string blockId)
        {
            return "# >>> " + ValidateBlockId(blockId) + " (managed block, do not edit) >>>";
        }

        public static string EndMarkerFor(string blockId)
        {
            return "# <<< " + ValidateBlockId(blockId) + " <<<";
        }

        /// <summary>
        /// Writes or updates the core block in <paramref name="filePath"/>, creating the file when absent.
        /// Returns true when the file content changed.
        /// </summary>
        public static bool UpsertFile(string filePath, IReadOnlyList<string> blockLines)
        {
            return UpsertFile(filePath, DefaultBlockId, blockLines);
        }

        /// <summary>Writes or updates the block <paramref name="blockId"/>; other blocks and user lines are untouched.</summary>
        public static bool UpsertFile(string filePath, string blockId, IReadOnlyList<string> blockLines)
        {
            var existing = File.Exists(filePath) ? File.ReadAllText(filePath, Utf8NoBom) : null;
            var updated = Upsert(existing, blockId, blockLines);
            if (existing == updated) return false;

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, updated, Utf8NoBom);
            return true;
        }

        /// <summary>Pure form of <see cref="UpsertFile(string, IReadOnlyList{string})"/> for the core block.</summary>
        public static string Upsert(string existingContent, IReadOnlyList<string> blockLines)
        {
            return Upsert(existingContent, DefaultBlockId, blockLines);
        }

        /// <summary>
        /// Pure form of <see cref="UpsertFile(string, string, IReadOnlyList{string})"/>:
        /// <paramref name="existingContent"/> is null when the file does not exist.
        /// </summary>
        public static string Upsert(string existingContent, string blockId, IReadOnlyList<string> blockLines)
        {
            if (blockLines == null) throw new ArgumentNullException(nameof(blockLines));
            var startMarker = StartMarkerFor(blockId);
            var endMarker = EndMarkerFor(blockId);

            var newline = DetectNewline(existingContent);
            var block = BuildBlock(startMarker, endMarker, blockLines, newline);

            if (string.IsNullOrEmpty(existingContent))
            {
                return block + newline;
            }

            var start = FindLineStart(existingContent, startMarker);
            if (start >= 0)
            {
                var endMarkerIndex = FindLineStart(existingContent, endMarker, start);
                if (endMarkerIndex >= 0)
                {
                    var endOfEndMarker = endMarkerIndex + endMarker.Length;
                    return existingContent.Substring(0, start) + block + existingContent.Substring(endOfEndMarker);
                }
                // Start marker without an end marker: treat the rest of the file as user content and append a fresh block.
            }

            var sb = new StringBuilder(existingContent);
            if (!existingContent.EndsWith("\n")) sb.Append(newline);
            // Keep one blank line between the previous content and the block for readability.
            if (!existingContent.EndsWith("\n\n") && !existingContent.EndsWith("\r\n\r\n")) sb.Append(newline);
            sb.Append(block);
            sb.Append(newline);
            return sb.ToString();
        }

        /// <summary>Returns the lines currently inside the core block, or null when there is no block.</summary>
        public static IReadOnlyList<string> ReadBlock(string content)
        {
            return ReadBlock(content, DefaultBlockId);
        }

        /// <summary>Returns the lines currently inside the block <paramref name="blockId"/>, or null when there is no such block.</summary>
        public static IReadOnlyList<string> ReadBlock(string content, string blockId)
        {
            var startMarker = StartMarkerFor(blockId);
            var endMarker = EndMarkerFor(blockId);
            if (string.IsNullOrEmpty(content)) return null;
            var start = FindLineStart(content, startMarker);
            if (start < 0) return null;
            var end = FindLineStart(content, endMarker, start);
            if (end < 0) return null;

            var inner = content.Substring(start + startMarker.Length, end - (start + startMarker.Length));
            var lines = inner.Replace("\r\n", "\n").Split('\n');
            var result = new List<string>();
            // The inner text starts and ends with the newline that surrounds the markers.
            for (var i = 1; i < lines.Length - 1; i++) result.Add(lines[i]);
            return result;
        }

        /// <summary>A block id is a package-name-like token: letters, digits, '.', '-' and '_'.</summary>
        internal static string ValidateBlockId(string blockId)
        {
            if (string.IsNullOrEmpty(blockId)) throw new ArgumentException("block id is required", nameof(blockId));
            foreach (var c in blockId)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_') continue;
                throw new ArgumentException("block id may contain only letters, digits, '.', '-' and '_': " + blockId, nameof(blockId));
            }
            return blockId;
        }

        internal static string DetectNewline(string content)
        {
            if (string.IsNullOrEmpty(content)) return "\n";
            var lf = content.IndexOf('\n');
            if (lf < 0) return "\n";
            return lf > 0 && content[lf - 1] == '\r' ? "\r\n" : "\n";
        }

        private static string BuildBlock(string startMarker, string endMarker, IReadOnlyList<string> lines, string newline)
        {
            var sb = new StringBuilder();
            sb.Append(startMarker);
            foreach (var line in lines)
            {
                sb.Append(newline);
                sb.Append(line ?? string.Empty);
            }
            sb.Append(newline);
            sb.Append(endMarker);
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
