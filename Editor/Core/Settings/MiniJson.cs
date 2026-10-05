using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shiori
{
    /// <summary>
    /// Deliberately small JSON reader/writer for Shiori's own settings files (see docs/adr/0001).
    /// Objects become <c>Dictionary&lt;string, object&gt;</c>, arrays <c>List&lt;object&gt;</c>,
    /// numbers <c>double</c> (or <c>long</c> when integral), plus string / bool / null.
    /// </summary>
    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var reader = new Reader(json);
            reader.SkipWhitespace();
            var value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd) throw reader.Error("unexpected trailing characters");
            return value;
        }

        public static string Serialize(object value, bool indented = true)
        {
            var sb = new StringBuilder();
            Write(sb, value, indented ? 0 : -1);
            if (indented) sb.Append('\n');
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, int indent)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case IDictionary<string, object> obj:
                    WriteObject(sb, obj, indent);
                    break;
                case IEnumerable<object> list:
                    WriteArray(sb, list, indent);
                    break;
                default:
                    throw new ArgumentException("unsupported JSON value type: " + value.GetType().FullName);
            }
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> obj, int indent)
        {
            if (obj.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            var first = true;
            foreach (var pair in obj)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, Child(indent));
                WriteString(sb, pair.Key);
                sb.Append(indent >= 0 ? ": " : ":");
                Write(sb, pair.Value, Child(indent));
            }
            NewLine(sb, indent);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable<object> list, int indent)
        {
            sb.Append('[');
            var first = true;
            foreach (var item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, Child(indent));
                Write(sb, item, Child(indent));
            }
            if (!first) NewLine(sb, indent);
            sb.Append(']');
        }

        /// <summary>Indent level for nested values; stays -1 (compact) when the parent is compact.</summary>
        private static int Child(int indent)
        {
            return indent < 0 ? -1 : indent + 1;
        }

        private static void NewLine(StringBuilder sb, int indent)
        {
            if (indent < 0) return;
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _pos;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _pos >= _text.Length;

            public FormatException Error(string message)
            {
                return new FormatException($"invalid JSON at offset {_pos}: {message}");
            }

            public void SkipWhitespace()
            {
                while (_pos < _text.Length)
                {
                    var c = _text[_pos];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _pos++;
                    else break;
                }
            }

            public object ReadValue()
            {
                if (AtEnd) throw Error("unexpected end of input");
                var c = _text[_pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ExpectLiteral("true"); return true;
                    case 'f': ExpectLiteral("false"); return false;
                    case 'n': ExpectLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("unexpected character '" + c + "'");
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _pos++; // {
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _pos++;
                    return result;
                }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw Error("expected property name");
                    var key = ReadString();
                    SkipWhitespace();
                    if (Peek() != ':') throw Error("expected ':'");
                    _pos++;
                    SkipWhitespace();
                    result[key] = ReadValue();
                    SkipWhitespace();
                    var c = Peek();
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == '}')
                    {
                        _pos++;
                        return result;
                    }
                    throw Error("expected ',' or '}'");
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _pos++;
                    return result;
                }
                while (true)
                {
                    SkipWhitespace();
                    result.Add(ReadValue());
                    SkipWhitespace();
                    var c = Peek();
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == ']')
                    {
                        _pos++;
                        return result;
                    }
                    throw Error("expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                _pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    var c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (AtEnd) throw Error("unterminated escape");
                    var e = _text[_pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length) throw Error("truncated \\u escape");
                            var hex = _text.Substring(_pos, 4);
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) throw Error("invalid \\u escape");
                            sb.Append((char)code);
                            _pos += 4;
                            break;
                        default:
                            throw Error("invalid escape '\\" + e + "'");
                    }
                }
            }

            private object ReadNumber()
            {
                var start = _pos;
                if (Peek() == '-') _pos++;
                var isInteger = true;
                while (!AtEnd)
                {
                    var c = _text[_pos];
                    if (c >= '0' && c <= '9')
                    {
                        _pos++;
                    }
                    else if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                    {
                        isInteger = false;
                        _pos++;
                    }
                    else
                    {
                        break;
                    }
                }
                var token = _text.Substring(start, _pos - start);
                if (isInteger && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)) return l;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                throw Error("invalid number '" + token + "'");
            }

            private void ExpectLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0) throw Error("expected '" + literal + "'");
                _pos += literal.Length;
            }

            private char Peek()
            {
                if (AtEnd) throw Error("unexpected end of input");
                return _text[_pos];
            }
        }
    }
}
