using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JellyMold.Core
{
    /// <summary>
    /// Minimal JSON reader (objects, arrays, strings, numbers, booleans, null) so level files
    /// can be parsed identically inside Unity and in plain .NET tools without extra packages.
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON text is null.");
            var reader = new Reader(json);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd) throw reader.Error("Unexpected trailing characters");
            return value;
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private sealed class Reader
        {
            private readonly string s;
            private int pos;

            public Reader(string s)
            {
                this.s = s;
            }

            public bool AtEnd => pos >= s.Length;

            public FormatException Error(string message)
            {
                return new FormatException(message + " at position " + pos + ".");
            }

            public void SkipWhitespace()
            {
                while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("Unexpected end of JSON");
                char c = s[pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(s, pos, word, 0, word.Length) != 0) throw Error("Expected '" + word + "'");
                pos += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>();
                pos++;
                SkipWhitespace();
                if (pos < s.Length && s[pos] == '}') { pos++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || s[pos] != '"') throw Error("Expected property name");
                    string name = ReadString();
                    SkipWhitespace();
                    if (AtEnd || s[pos] != ':') throw Error("Expected ':'");
                    pos++;
                    result[name] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated object");
                    if (s[pos] == ',') { pos++; continue; }
                    if (s[pos] == '}') { pos++; return result; }
                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                pos++;
                SkipWhitespace();
                if (pos < s.Length && s[pos] == ']') { pos++; return result; }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated array");
                    if (s[pos] == ',') { pos++; continue; }
                    if (s[pos] == ']') { pos++; return result; }
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                pos++;
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    char c = s[pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("Unterminated escape");
                    char e = s[pos++];
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
                            if (pos + 4 > s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 4;
                            break;
                        default: throw Error("Bad escape '\\" + e + "'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = pos;
                if (s[pos] == '-') pos++;
                while (pos < s.Length && "0123456789.eE+-".IndexOf(s[pos]) >= 0) pos++;
                string text = s.Substring(start, pos - start);
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error("Bad number '" + text + "'");
                return value;
            }
        }
    }
}
