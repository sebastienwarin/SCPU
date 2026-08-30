using System.Text;
using System.Text.RegularExpressions;

namespace SCPU.Assembler
{
    /// <summary>
    /// Provides utility methods for parsing and normalizing string literals and numeric values in S-CPU assembly source code.
    /// </summary>
    internal static class AssemblyStringUtils
    {
        private static readonly Regex _charLiteralRegex = new(@"'((?:\\.|[^'\\]))'", RegexOptions.Compiled);

        private static readonly Regex _hexLiteralRegex = new(@"0x[0-9A-Fa-f_]+", RegexOptions.Compiled);

        private static readonly Regex _binaryLiteralRegex = new(@"0b[01_]+", RegexOptions.Compiled);

        private static readonly Regex _decimalLiteralRegex = new(@"\b\d[\d_]*\b", RegexOptions.Compiled);

        /// <summary>
        /// Converts all numeric literals in an expression to their decimal representation.
        /// Supports character literals, hex (0x), binary (0b), and decimal with underscore separators.
        /// Processing order: char → hex → binary → decimal (prevents partial matches).
        /// </summary>
        /// <param name="expression">Input expression string, possibly containing numeric literals and operators.</param>
        /// <returns>Expression string with all numeric literals converted to decimal equivalents.</returns>
        internal static string ReplaceNumericLiterals(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return expression;

            expression = _charLiteralRegex.Replace(expression, ReplaceCharLiteral);
            expression = _hexLiteralRegex.Replace(expression, ReplaceHexLiteral);
            expression = _binaryLiteralRegex.Replace(expression, ReplaceBinaryLiteral);
            expression = _decimalLiteralRegex.Replace(expression, ReplaceDecimalLiteral);

            return expression;
        }

        private static string ReplaceCharLiteral(Match match)
        {
            var token = match.Groups[1].Value;
            int code = DecodeCharToken(token);
            return code.ToString();
        }

        private static string ReplaceHexLiteral(Match match)
        {
            string cleaned = match.Value.Replace("_", string.Empty);
            return Convert.ToUInt32(cleaned, 16).ToString();
        }

        private static string ReplaceBinaryLiteral(Match match)
        {
            string cleaned = match.Value[2..].Replace("_", string.Empty);
            return Convert.ToUInt32(cleaned, 2).ToString();
        }

        private static string ReplaceDecimalLiteral(Match match)
        {
            string cleaned = match.Value.Replace("_", string.Empty);
            return int.Parse(cleaned).ToString();
        }

        /// <summary>
        /// Tries to parse a standalone numeric literal (hex 0x, binary 0b, or decimal) into an unsigned 32-bit value.
        /// Underscore separators and an optional leading <c>+</c> are accepted. Returns <c>false</c> for any non-numeric input.
        /// </summary>
        /// <param name="expression">The literal string to parse.</param>
        /// <param name="result">The parsed value, or zero on failure.</param>
        /// <returns><c>true</c> if successfully parsed; otherwise <c>false</c>.</returns>
        internal static bool TryParseDirectNumeric(string expression, out uint result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(expression))
                return false;

            // Trim and accept an optional leading '+'
            string trimmed = expression.Trim();
            int start = 0;
            if (trimmed.Length > 0 && trimmed[0] == '+')
                start = 1;

            if (start >= trimmed.Length)
                return false;

            // Work on the remaining span
            ReadOnlySpan<char> span = trimmed.AsSpan(start);

            // Hex: 0x...
            if (span.Length >= 2 && (span[0] == '0') && (span[1] == 'x' || span[1] == 'X'))
            {
                string cleaned = span[2..].ToString().Replace("_", string.Empty);
                if (cleaned.Length == 0)
                    return false;
                return uint.TryParse(cleaned, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out result);
            }

            // Binary: 0b...
            if (span.Length >= 2 && (span[0] == '0') && (span[1] == 'b' || span[1] == 'B'))
            {
                ReadOnlySpan<char> binSpan = span[2..];
                if (binSpan.Length == 0)
                    return false;

                // remove underscores and validate characters
                var sb = new StringBuilder(binSpan.Length);
                foreach (char c in binSpan)
                {
                    if (c == '_') continue;
                    if (c != '0' && c != '1') return false;
                    sb.Append(c);
                }

                if (sb.Length == 0)
                    return false;

                // safe to parse (validated)
                try
                {
                    result = Convert.ToUInt32(sb.ToString(), 2);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // Decimal: starts with a digit after optional '+'
            if (char.IsDigit(span[0]))
            {
                string cleaned = span.ToString().Replace("_", string.Empty);
                if (cleaned.Length == 0)
                    return false;
                return uint.TryParse(cleaned, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out result);
            }

            return false;
        }

        /// <summary>
        /// Splits comma-separated data elements while preserving commas inside quoted strings and character literals.
        /// Uses a state machine that honours <c>\"</c> and <c>\'</c> escape sequences.
        /// </summary>
        /// <param name="valuePart">Comma-separated value string, e.g. from a <c>#d16</c> directive.</param>
        /// <returns>List of individual elements, each trimmed of whitespace.</returns>
        internal static List<string> SplitDataElements(string valuePart)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inString = false, inChar = false;

            for (int i = 0; i < valuePart.Length; i++)
            {
                char c = valuePart[i];

                if (inString)
                {
                    sb.Append(c);
                    if (c == '"' && (i == 0 || valuePart[i - 1] != '\\'))
                        inString = false;
                }
                else if (inChar)
                {
                    sb.Append(c);
                    if (c == '\'' && (i == 0 || valuePart[i - 1] != '\\'))
                        inChar = false;
                }
                else
                {
                    if (c == ',')
                    {
                        result.Add(sb.ToString().Trim());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(c);
                        if (c == '"')
                            inString = true;
                        else if (c == '\'')
                            inChar = true;
                    }
                }
            }

            var last = sb.ToString().Trim();
            if (!string.IsNullOrEmpty(last))
                result.Add(last);

            return result;
        }

        /// <summary>
        /// Unescapes standard C-style escape sequences (<c>\n</c>, <c>\r</c>, <c>\t</c>, <c>\\</c>, <c>\'</c>, <c>\"</c>, <c>\0</c>).
        /// Unknown sequences are passed through as-is. Outer quotes must be stripped before calling.
        /// </summary>
        /// <param name="s">Literal content to unescape.</param>
        /// <returns>String with all recognized escape sequences replaced.</returns>
        internal static string UnescapeString(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length;)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char c = s[i];
                    switch (c)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '\\': sb.Append('\\'); break;
                        case '\'': sb.Append('\''); break;
                        case '"': sb.Append('"'); break;
                        case '0': sb.Append('\0'); break;
                        default: sb.Append(c); break;
                    }
                    i++;
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static int DecodeCharToken(string token)
        {
            if (token.Length == 1 && token[0] != '\\')
                return token[0];

            if (token.Length >= 2 && token[0] == '\\')
            {
                char esc = token[1];
                switch (esc)
                {
                    case '\\': return '\\';
                    case '\'': return '\'';
                    case '\"': return '\"';
                    case '0': return 0;
                    case 'a': return 0x07; // BEL (bell)
                    case 'b': return 0x08; // BS (backspace)
                    case 't': return 0x09; // TAB
                    case 'n': return 0x0A; // LF (newline)
                    case 'v': return 0x0B; // VT (vertical tab)
                    case 'f': return 0x0C; // FF (form feed)
                    case 'r': return 0x0D; // CR (carriage return)

                    case 'x':
                    {
                        // Hex escape: \xHH (1-4 hex digits)
                        string hex = token[2..];
                        int len = 0;
                        while (len < hex.Length && len < 4 && IsHex(hex[len]))
                            len++;
                        return len == 0 ? 'x' : Convert.ToInt32(hex[..len], 16);
                    }

                    case 'u':
                    {
                        // Unicode escape: \uHHHH (exactly 4 hex digits)
                        if (token.Length >= 6 && token[2..6].All(IsHex))
                            return Convert.ToInt32(token[2..6], 16);
                        return 'u';
                    }

                    case 'U':
                    {
                        // Unicode escape: \UHHHHHHHH (exactly 8 hex digits)
                        if (token.Length >= 10 && token[2..10].All(IsHex))
                            return Convert.ToInt32(token[2..10], 16);
                        return 'U';
                    }

                    default:
                        // Unknown escape: return the escaped character itself
                        return esc;
                }
            }

            return token[0];
        }

        private static bool IsHex(char c)
            => (c >= '0' && c <= '9') ||
               (c >= 'a' && c <= 'f') ||
               (c >= 'A' && c <= 'F');
    }
}
