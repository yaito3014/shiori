using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shiori
{
    /// <summary>
    /// Reads the YAML subset Unity uses for scenes, prefabs, materials and other assets (ADR 0005).
    /// Supported: the <c>--- !u!&lt;class&gt; &amp;&lt;fileID&gt; [stripped]</c> object headers, indented block
    /// maps and sequences (including Unity's habit of writing a sequence at its key's indent), flow maps
    /// and flow sequences, plain, single- and double-quoted scalars (also when wrapped over several
    /// lines) and literal / folded block scalars. Anything else is kept as raw text rather than failing,
    /// because a partly understood diff is still more useful than none.
    /// </summary>
    public static class UnityYamlParser
    {
        private readonly struct Line
        {
            public readonly int Indent;
            public readonly string Text;

            public Line(int indent, string text)
            {
                Indent = indent;
                Text = text;
            }
        }

        /// <summary>Parses a whole file into its objects, in file order.</summary>
        public static IReadOnlyList<UnityYamlDocument> Parse(string text)
        {
            var documents = new List<UnityYamlDocument>();
            if (string.IsNullOrEmpty(text)) return documents;

            string header = null;
            var body = new List<Line>();
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.StartsWith("---", StringComparison.Ordinal))
                {
                    if (header != null) documents.Add(BuildDocument(header, body));
                    header = raw;
                    body = new List<Line>();
                    continue;
                }
                if (header == null) continue; // %YAML / %TAG directives
                var trimmed = raw.TrimEnd();
                if (trimmed.Length == 0) continue;
                var indent = 0;
                while (indent < trimmed.Length && trimmed[indent] == ' ') indent++;
                body.Add(new Line(indent, trimmed.Substring(indent)));
            }
            if (header != null) documents.Add(BuildDocument(header, body));
            return documents;
        }

        /// <summary>Parses a block of YAML without document headers (used by tests and for single values).</summary>
        public static YamlNode ParseBlock(string text)
        {
            var lines = new List<Line>();
            foreach (var raw in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = raw.TrimEnd();
                if (trimmed.Length == 0) continue;
                var indent = 0;
                while (indent < trimmed.Length && trimmed[indent] == ' ') indent++;
                lines.Add(new Line(indent, trimmed.Substring(indent)));
            }
            if (lines.Count == 0) return YamlNode.Empty;
            var index = 0;
            return ParseNode(lines, ref index, lines[0].Indent);
        }

        private static UnityYamlDocument BuildDocument(string header, List<Line> lines)
        {
            // "--- !u!1 &1234567890" or "--- !u!4 &400000 stripped"
            var classId = 0;
            long fileId = 0;
            var stripped = false;
            foreach (var part in header.Substring(3).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("!u!", StringComparison.Ordinal)) int.TryParse(part.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out classId);
                else if (part.StartsWith("&", StringComparison.Ordinal)) long.TryParse(part.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out fileId);
                else if (part == "stripped") stripped = true;
            }

            var root = lines.Count == 0 ? YamlNode.Map(null) : ParseNodeSafe(lines);
            // The document is a one-entry map: "TypeName:" followed by the fields.
            if (root.Kind == YamlNodeKind.Map && root.Entries.Count >= 1)
            {
                var first = root.Entries[0];
                return new UnityYamlDocument(classId, fileId, stripped, first.Key, first.Value.Kind == YamlNodeKind.Map ? first.Value : YamlNode.Map(null));
            }
            return new UnityYamlDocument(classId, fileId, stripped, string.Empty, YamlNode.Map(null));
        }

        private static YamlNode ParseNodeSafe(List<Line> lines)
        {
            try
            {
                var index = 0;
                return ParseNode(lines, ref index, lines[0].Indent);
            }
            catch (Exception)
            {
                // Never let one odd object break the whole file.
                var sb = new StringBuilder();
                foreach (var line in lines) sb.Append(new string(' ', line.Indent)).Append(line.Text).Append('\n');
                return YamlNode.Map(new[] { new KeyValuePair<string, YamlNode>("(raw)", YamlNode.Scalar(sb.ToString())) });
            }
        }

        private static bool IsSequenceItem(string text)
        {
            return text == "-" || text.StartsWith("- ", StringComparison.Ordinal);
        }

        /// <summary>Parses the block that starts at <paramref name="index"/> and has <paramref name="indent"/>.</summary>
        private static YamlNode ParseNode(List<Line> lines, ref int index, int indent)
        {
            if (index >= lines.Count) return YamlNode.Empty;
            return IsSequenceItem(lines[index].Text) ? ParseSequence(lines, ref index, indent) : ParseMap(lines, ref index, indent);
        }

        private static YamlNode ParseSequence(List<Line> lines, ref int index, int indent)
        {
            var items = new List<YamlNode>();
            while (index < lines.Count && lines[index].Indent == indent && IsSequenceItem(lines[index].Text))
            {
                var rest = lines[index].Text.Length > 1 ? lines[index].Text.Substring(2) : string.Empty;
                var itemIndent = indent + 2;
                if (rest.Length == 0)
                {
                    index++;
                    items.Add(index < lines.Count && lines[index].Indent > indent ? ParseNode(lines, ref index, lines[index].Indent) : YamlNode.Empty);
                    continue;
                }
                // "- key: value" starts a map whose further keys sit at indent + 2; "- value" is a scalar or flow node.
                if (SplitKey(rest, out _, out _))
                {
                    lines[index] = new Line(itemIndent, rest);
                    items.Add(ParseMap(lines, ref index, itemIndent));
                }
                else
                {
                    index++;
                    items.Add(ParseInlineWithContinuation(rest, lines, ref index, indent));
                }
            }
            return YamlNode.Sequence(items);
        }

        private static YamlNode ParseMap(List<Line> lines, ref int index, int indent)
        {
            var entries = new List<KeyValuePair<string, YamlNode>>();
            while (index < lines.Count && lines[index].Indent == indent && !IsSequenceItem(lines[index].Text))
            {
                if (!SplitKey(lines[index].Text, out var key, out var rest))
                {
                    // Not "key: value": keep the line as raw text under its own key.
                    entries.Add(new KeyValuePair<string, YamlNode>("(raw)", YamlNode.Scalar(lines[index].Text)));
                    index++;
                    continue;
                }
                index++;
                YamlNode value;
                if (rest.Length == 0)
                {
                    // The value is the following block: deeper, or a sequence written at the key's own indent.
                    if (index < lines.Count && (lines[index].Indent > indent || lines[index].Indent == indent && IsSequenceItem(lines[index].Text)))
                    {
                        value = ParseNode(lines, ref index, lines[index].Indent);
                    }
                    else
                    {
                        value = YamlNode.Empty;
                    }
                }
                else if (rest == "|" || rest == ">" || rest.StartsWith("|", StringComparison.Ordinal) || rest.StartsWith(">", StringComparison.Ordinal))
                {
                    value = ParseBlockScalar(rest[0] == '|', lines, ref index, indent);
                }
                else
                {
                    value = ParseInlineWithContinuation(rest, lines, ref index, indent);
                }
                entries.Add(new KeyValuePair<string, YamlNode>(key, value));
            }
            return YamlNode.Map(entries);
        }

        /// <summary>
        /// An inline value, joined with deeper continuation lines while it is incomplete (open quote or
        /// bracket) or, for plain scalars, while the following lines are more deeply indented.
        /// </summary>
        private static YamlNode ParseInlineWithContinuation(string first, List<Line> lines, ref int index, int indent)
        {
            var text = first;
            while (index < lines.Count && lines[index].Indent > indent && (IsIncomplete(text) || IsPlain(text)))
            {
                text = Fold(text, lines[index].Text);
                index++;
            }
            return ParseInline(text);
        }

        private static string Fold(string text, string next)
        {
            // Inside quotes a line break folds to a space; Unity breaks long strings at spaces.
            return text + " " + next;
        }

        private static bool IsPlain(string text)
        {
            var c = text.Length == 0 ? '\0' : text[0];
            return c != '"' && c != '\'' && c != '{' && c != '[';
        }

        /// <summary>True while a quote, brace or bracket opened in <paramref name="text"/> is still open.</summary>
        private static bool IsIncomplete(string text)
        {
            if (text.Length == 0) return false;
            if (text[0] == '"')
            {
                for (var i = 1; i < text.Length; i++)
                {
                    if (text[i] == '\\') { i++; continue; }
                    if (text[i] == '"') return false;
                }
                return true;
            }
            if (text[0] == '\'')
            {
                for (var i = 1; i < text.Length; i++)
                {
                    if (text[i] != '\'') continue;
                    if (i + 1 < text.Length && text[i + 1] == '\'') { i++; continue; }
                    return false;
                }
                return true;
            }
            if (text[0] == '{' || text[0] == '[')
            {
                var depth = 0;
                var quote = '\0';
                foreach (var c in text)
                {
                    if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                    if (c == '"' || c == '\'') quote = c;
                    else if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') depth--;
                }
                return depth > 0;
            }
            return false;
        }

        private static YamlNode ParseBlockScalar(bool literal, List<Line> lines, ref int index, int indent)
        {
            var parts = new List<string>();
            while (index < lines.Count && lines[index].Indent > indent)
            {
                parts.Add(lines[index].Text);
                index++;
            }
            return YamlNode.Scalar(string.Join(literal ? "\n" : " ", parts));
        }

        /// <summary>Splits "key: value" or "key:" at the first ": " outside quotes and brackets.</summary>
        private static bool SplitKey(string text, out string key, out string rest)
        {
            key = null;
            rest = null;
            if (text.Length == 0 || text[0] == '{' || text[0] == '[' || text[0] == '"' || text[0] == '\'') return false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '{' || c == '[' || c == '"' || c == '\'') return false;
                if (c != ':') continue;
                if (i + 1 == text.Length || text[i + 1] == ' ')
                {
                    key = text.Substring(0, i).Trim();
                    rest = i + 1 == text.Length ? string.Empty : text.Substring(i + 2).Trim();
                    return key.Length > 0;
                }
            }
            return false;
        }

        // ---- inline values ----

        public static YamlNode ParseInline(string text)
        {
            var position = 0;
            var node = ReadInline(text.Trim(), ref position, topLevel: true);
            return node;
        }

        private static YamlNode ReadInline(string text, ref int position, bool topLevel)
        {
            SkipSpaces(text, ref position);
            if (position >= text.Length) return YamlNode.Empty;
            var c = text[position];
            if (c == '{') return ReadFlowMap(text, ref position);
            if (c == '[') return ReadFlowSequence(text, ref position);
            if (c == '"') return YamlNode.Scalar(ReadDoubleQuoted(text, ref position));
            if (c == '\'') return YamlNode.Scalar(ReadSingleQuoted(text, ref position));
            if (topLevel)
            {
                var value = text.Substring(position).Trim();
                position = text.Length;
                return YamlNode.Scalar(value);
            }
            // Inside a flow collection a plain scalar ends at ',' '}' or ']'.
            var start = position;
            while (position < text.Length && text[position] != ',' && text[position] != '}' && text[position] != ']') position++;
            return YamlNode.Scalar(text.Substring(start, position - start).Trim());
        }

        private static YamlNode ReadFlowMap(string text, ref int position)
        {
            position++; // {
            var entries = new List<KeyValuePair<string, YamlNode>>();
            while (true)
            {
                SkipSpaces(text, ref position);
                if (position >= text.Length) break;
                if (text[position] == '}') { position++; break; }
                if (text[position] == ',') { position++; continue; }
                var keyStart = position;
                while (position < text.Length && text[position] != ':' && text[position] != ',' && text[position] != '}') position++;
                var key = text.Substring(keyStart, position - keyStart).Trim();
                if (position < text.Length && text[position] == ':')
                {
                    position++;
                    entries.Add(new KeyValuePair<string, YamlNode>(key, ReadInline(text, ref position, topLevel: false)));
                }
                else
                {
                    entries.Add(new KeyValuePair<string, YamlNode>(key, YamlNode.Empty));
                }
            }
            return YamlNode.Map(entries);
        }

        private static YamlNode ReadFlowSequence(string text, ref int position)
        {
            position++; // [
            var items = new List<YamlNode>();
            while (true)
            {
                SkipSpaces(text, ref position);
                if (position >= text.Length) break;
                if (text[position] == ']') { position++; break; }
                if (text[position] == ',') { position++; continue; }
                items.Add(ReadInline(text, ref position, topLevel: false));
            }
            return YamlNode.Sequence(items);
        }

        private static string ReadDoubleQuoted(string text, ref int position)
        {
            position++; // "
            var sb = new StringBuilder();
            while (position < text.Length)
            {
                var c = text[position++];
                if (c == '"') break;
                if (c != '\\' || position >= text.Length)
                {
                    sb.Append(c);
                    continue;
                }
                var e = text[position++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '0': sb.Append('\0'); break;
                    case ' ': sb.Append(' '); break; // escaped line-fold space
                    case 'u':
                        if (position + 4 <= text.Length && int.TryParse(text.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            sb.Append((char)code);
                            position += 4;
                        }
                        else
                        {
                            sb.Append("\\u");
                        }
                        break;
                    default: sb.Append(e); break; // \" \\ \/
                }
            }
            return sb.ToString();
        }

        private static string ReadSingleQuoted(string text, ref int position)
        {
            position++; // '
            var sb = new StringBuilder();
            while (position < text.Length)
            {
                var c = text[position++];
                if (c != '\'')
                {
                    sb.Append(c);
                    continue;
                }
                if (position < text.Length && text[position] == '\'')
                {
                    sb.Append('\'');
                    position++;
                    continue;
                }
                break;
            }
            return sb.ToString();
        }

        private static void SkipSpaces(string text, ref int position)
        {
            while (position < text.Length && text[position] == ' ') position++;
        }
    }
}
