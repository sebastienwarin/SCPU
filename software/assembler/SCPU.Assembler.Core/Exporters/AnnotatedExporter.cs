using SCPU.Assembler.Model;
using System.Text;

namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Exports an <see cref="AssemblyResult"/> as an aligned, human-readable table:
    /// columns include address, word value, prettified source line, labels, and the
    /// original raw source (shown once per macro-expansion group).
    /// </summary>
    public sealed class AnnotatedExporter : IAssemblyExporter
    {
        /// <summary>
        /// Gets the format identifier for this exporter (<see cref="OutputFormat.Annotated"/>).
        /// </summary>
        public OutputFormat Format => OutputFormat.Annotated;

        /// <summary>
        /// Converts the <see cref="AssemblyResult"/> into a textual annotated listing.
        /// Operands are prettified: numeric values render in hex and use symbols when available
        /// (merged label/constant map).
        /// </summary>
        /// <param name="result">The completed assembly result (words, labels, constants).</param>
        /// <returns>UTF-8 bytes of the formatted table.</returns>
        public byte[] Convert(AssemblyResult result)
        {
            var symbolMap = BuildReverseSymbolMap(result.Labels, result.Constants);
            var rows = new List<Row>(result.FinalWords.Count);

            // Track the emitted word index per Line for data directives (#d16/#d32/#d)
            var dataEmitWordIndex = new Dictionary<Line, int>(ReferenceEqualityComparer.Instance);

            // Show RawContent only for the first row of a consecutive identical source.
            object? lastSourceObject = null;

            for (int i = 0; i < result.FinalWords.Count; i++)
            {
                var (src, word) = result.FinalWords[i];

                // Format address and value
                string addrStr = $"0x{i:X4}";
                string valStr  = $"0x{word:X4}";

                string lineStr = string.Empty;
                string labelStr = string.Empty;
                string sourceStr = string.Empty;

                if (src is Line line)
                {
                    // Data directives: show only the current element for this word
                    if (TryFormatDataElement(line, dataEmitWordIndex, out var elemText, out var wordIdxForLine))
                    {
                        lineStr = elemText;

                        // Label index
                        if (line.Labels is { Count: > 0 })
                        {
                            var primary = line.Labels[0];
                            labelStr = (wordIdxForLine == 0)
                                ? string.Join(", ", line.Labels)
                                : $"{primary}[{wordIdxForLine}]";
                        }
                    }
                    else
                    {
                        // Prettify operands (hex + symbol substitution)
                        lineStr = PrettyFormatLine(line.Content ?? string.Empty, symbolMap);

                        // Labels
                        if (line.Labels is { Count: > 0 })
                            labelStr = string.Join(", ", line.Labels);
                    }

                    // Only the first line of a macro-expanded block shows RawContent
                    var currentSourceObj = (object?)line.Source;
                    if (!ReferenceEquals(lastSourceObject, currentSourceObj))
                    {
                        sourceStr = TryGetRawContent(line) ?? string.Empty;
                        lastSourceObject = currentSourceObj;
                    }
                }
                else
                {
                    // Injected constant: the string IS the label; show decoded value in the line column.
                    if (src is string constLabel)
                    {
                        labelStr = constLabel;
                        lineStr  = FormatConstantLineValue(word);
                    }
                    else
                    {
                        lineStr = src?.ToString() ?? string.Empty;
                    }
                    lastSourceObject = null;
                }

                rows.Add(new Row(addrStr, valStr, lineStr, labelStr, sourceStr));
            }

            // Column headers
            const string HAddr = "addr";
            const string HValue = "value";
            const string HLine = "line";
            const string HLabel = "label";
            const string HSource = "source";

            // Compute max widths
            int wAddr   = Math.Max(HAddr.Length,   rows.Count == 0 ? 0 : rows.Max(r => r.Addr.Length));
            int wValue  = Math.Max(HValue.Length,  rows.Count == 0 ? 0 : rows.Max(r => r.Value.Length));
            int wLine   = Math.Max(HLine.Length,   rows.Count == 0 ? 0 : rows.Max(r => r.Line.Length));
            int wLabel  = Math.Max(HLabel.Length,  rows.Count == 0 ? 0 : rows.Max(r => r.Label.Length));
            int wSource = Math.Max(HSource.Length, rows.Count == 0 ? 0 : rows.Max(r => r.Source.Length));

            // Render the table
            var sb = new StringBuilder(rows.Count * 64);

            // Header row
            sb.Append(' ')
              .Append(HAddr.PadRight(wAddr)).Append(" | ")
              .Append(HValue.PadRight(wValue)).Append(" | ")
              .Append(HLine.PadRight(wLine)).Append(" | ")
              .Append(HLabel.PadRight(wLabel)).Append(" | ")
              .Append(HSource.PadRight(wSource)).AppendLine();

            sb.AppendLine();

            // Data rows
            foreach (var r in rows)
            {
                sb.Append(' ')
                  .Append(r.Addr.PadRight(wAddr)).Append(" | ")
                  .Append(r.Value.PadRight(wValue)).Append(" | ")
                  .Append(r.Line.PadRight(wLine)).Append(" | ")
                  .Append(r.Label.PadRight(wLabel)).Append(" | ")
                  .Append(r.Source.PadRight(wSource)).AppendLine();
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Prettifies a full line: splits mnemonic/operands, formats each operand using the merged symbol map.
        /// Also normalizes the JCC operand display to hide the long jump complexity.
        /// </summary>
        private static string PrettyFormatLine(string content, Dictionary<uint, string> symbols)
        {
            if (string.IsNullOrWhiteSpace(content)) return content;
            var i = content.IndexOf(' ');
            if (i < 0) return content;

            var mnemonic = content[..i];
            var opsSpan  = content[(i + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(opsSpan)) return content;

            var ops = opsSpan.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (int k = 0; k < ops.Length; k++)
            {
                ops[k] = PrettyFormatOperand(ops[k], symbols);

                // JCC visual tweak: immediate means "PC <- imm"; absolute/indirect often read nicer as "@..."
                if (mnemonic.Equals("jcc", StringComparison.OrdinalIgnoreCase))
                {
                    if (ops[k].StartsWith('#'))
                        ops[k] = ops[k][1..];     // drop '#' for JCC immediates
                    else
                        ops[k] = "@" + ops[k];    // hint indirection visually
                }
            }

            return $"{mnemonic} {string.Join(", ", ops)}";
        }

        /// <summary>
        /// Formats a single operand with a unified symbol map
        /// </summary>
        private static string PrettyFormatOperand(string op, Dictionary<uint, string> symbols)
        {
            if (string.IsNullOrWhiteSpace(op)) return op;
            op = op.Trim();

            static string Paren(string prefix, string inner, Dictionary<uint, string> syms)
            {
                var raw = inner.Trim();
                if (TryParseNumber(raw, out uint v))
                    return $"{prefix}({FormatValue(v, syms)})";
                return $"{prefix}({inner})";
            }

            // Parenthesized
            if (op.StartsWith("#(") && op.EndsWith(")")) return Paren("#", op[2..^1], symbols);
            if (op.StartsWith("@(") && op.EndsWith(")")) return Paren("@", op[2..^1], symbols);
            if (op.StartsWith('(')  && op.EndsWith(')')) return Paren(string.Empty, op[1..^1], symbols);

            // Simple forms
            if (op.StartsWith('#'))
            {
                var raw = op[1..].Trim();
                if (TryParseNumber(raw, out uint v)) return $"#{FormatValue(v, symbols)}";
                return op;
            }
            if (op.StartsWith('@'))
            {
                var raw = op[1..].Trim();
                if (TryParseNumber(raw, out uint v)) return $"@{FormatValue(v, symbols)}";
                return op;
            }

            if (TryParseNumber(op, out uint abs)) return FormatValue(abs, symbols);
            return op;

            static string FormatValue(uint v, Dictionary<uint, string> syms)
                => syms.TryGetValue(v, out var name) ? name : $"0x{v:X}";
        }

        /// <summary>
        /// Parses decimal / 0xHEX / 0bBIN numbers; underscores are ignored.
        /// </summary>
        private static bool TryParseNumber(string s, out uint value)
        {
            s = s.Replace("_", "").Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out value);
            }
            if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                value = 0;
                for (int i = 2; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '0' || c == '1')
                        value = (value << 1) | (uint)(c - '0');
                    else { value = 0; return false; }
                }
                return true;
            }
            return uint.TryParse(s, out value);
        }

        /// <summary>
        /// Builds a merged symbol map from constants and labels.
        /// </summary>
        private static Dictionary<uint, string> BuildReverseSymbolMap(
             IDictionary<string, uint> labels,
             IDictionary<string, int> constants)
        {
            var map = new Dictionary<uint, string>();

            // constants first
            foreach (var (name, val) in constants)
            {
                var u = unchecked((uint)val);
                if (!map.TryGetValue(u, out var exist))
                    map[u] = name;
                else
                    map[u] = PickBetterName(exist, name);
            }

            // then labels (override when more meaningful)
            foreach (var (name, addr) in labels)
            {
                if (!map.TryGetValue(addr, out var exist))
                    map[addr] = name;
                else
                    map[addr] = PickBetterName(exist, name);
            }

            return map;

            static string PickBetterName(string a, string b)
            {
                bool agen = a.StartsWith("__", StringComparison.Ordinal);
                bool bgen = b.StartsWith("__", StringComparison.Ordinal);
                if (agen && !bgen) return b;       // prefer non-generated
                if (!agen && bgen) return a;
                return a.Length <= b.Length ? a : b; // else prefer shorter
            }
        }

        /// <summary>
        /// Tries to read the original raw source text for a line (macro-expanded group leader);
        /// returns null when unavailable.
        /// </summary>
        private static string? TryGetRawContent(Line line)
        {
            try
            {
                var srcObj = (object?)line.Source;
                if (srcObj is null) return null;

                var t = srcObj.GetType();

                var p = t.GetProperty("RawContent");
                if (p is not null) return p.GetValue(srcObj) as string;

                var f = t.GetField("RawContent");
                if (f is not null) return f.GetValue(srcObj) as string;

                if (srcObj is string s) return s;

                return srcObj.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// If the line contains a sized/unsized data directive (#d16/#d32/#d),
        /// formats only the current element associated with the word being emitted.
        /// </summary>
        private static bool TryFormatDataElement(Line line, Dictionary<Line, int> perLineWordIndex, out string elementText, out int currentWordIndexForLine)
        {
            elementText = string.Empty;
            currentWordIndexForLine = 0;

            var content = line.Content?.Trim();
            if (string.IsNullOrEmpty(content))
                return false;

            // Detect directive token position (first occurrence)
            int posD16 = content.IndexOf(AssemblerConstants.D16Directive, StringComparison.OrdinalIgnoreCase);
            int posD32 = content.IndexOf(AssemblerConstants.D32Directive, StringComparison.OrdinalIgnoreCase);
            int posD = content.IndexOf(AssemblerConstants.DataDirective, StringComparison.OrdinalIgnoreCase);

            // Choose the earliest valid directive occurrence
            int pos = int.MaxValue;
            string directive = string.Empty;
            if (posD16 >= 0 && posD16 < pos) { pos = posD16; directive = AssemblerConstants.D16Directive; }
            if (posD32 >= 0 && posD32 < pos) { pos = posD32; directive = AssemblerConstants.D32Directive; }
            // Important: ensure #d is not matching #d16/#d32 again; check exact token
            if (posD >= 0 && (directive == string.Empty || posD < pos) && IsPlainD(content, posD))
            {
                pos = posD; directive = AssemblerConstants.DataDirective;
            }

            if (string.IsNullOrEmpty(directive))
                return false; // not a data directive this line

            // Extract the list portion AFTER the directive token
            var listPart = content[(pos + directive.Length)..].Trim();
            if (string.IsNullOrEmpty(listPart))
                return false;

            // Split elements respecting quotes
            var tokens = SplitCsvRespectingQuotes(listPart);
            if (tokens.Count == 0)
                return false;

            // Retrieve current word index for this line (how many words already emitted)
            if (!perLineWordIndex.TryGetValue(line, out var wordIndex))
                wordIndex = 0;

            // Expose to caller for label rendering 
            currentWordIndexForLine = wordIndex;

            // Find which token/sub-index corresponds to this wordIndex
            // Each token contributes N words depending on directive and token type
            int cumulative = 0;
            for (int t = 0; t < tokens.Count; t++)
            {
                var token = tokens[t].Trim();
                int tokenWords = GetTokenWordCount(directive, token);

                if (wordIndex < cumulative + tokenWords)
                {
                    // This is the token to display. Compute the sub-index within the token (0..tokenWords-1)
                    int subIndex = wordIndex - cumulative;

                    // Produce display text for this token at the given sub-index.
                    // For multi-word tokens (e.g., 32-bit number), we display only on the first sub-word.
                    elementText = FormatDataTokenDisplay(directive, token, subIndex);
                    break;
                }

                cumulative += tokenWords;
            }

            // Increment the per-line index (advance by one word)
            perLineWordIndex[line] = wordIndex + 1;
            return true;

            static bool IsPlainD(string s, int pos)
            {
                // s[pos..] starts with "#d" but may be "#d16"/"#d32". Ensure the next char is not '1' or '3'
                if (pos < 0 || pos + 2 >= s.Length) return false;
                if (!s.AsSpan(pos, 2).SequenceEqual(AssemblerConstants.DataDirective.AsSpan())) return false;
                if (pos + 3 <= s.Length)
                {
                    char next = s[pos + 2];
                    return next != '1' && next != '3'; // exclude #d16/#d32
                }
                return true;
            }
        }

        /// <summary>
        /// Splits a comma-separated list while respecting double-quoted substrings.
        /// </summary>
        private static List<string> SplitCsvRespectingQuotes(string input)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
                list.Add(sb.ToString());

            // Clean
            for (int i = 0; i < list.Count; i++)
                list[i] = list[i].Trim();

            return list;
        }

        /// <summary>
        /// Returns how many 16-bit words this token contributes, depending on directive.
        /// </summary>
        private static int GetTokenWordCount(string directive, string token)
        {
            bool isString = IsQuoted(token);
            if (directive.Equals(AssemblerConstants.D16Directive, StringComparison.OrdinalIgnoreCase))
                return isString ? CountStringChars(token) : 1;

            if (directive.Equals(AssemblerConstants.D32Directive, StringComparison.OrdinalIgnoreCase))
                return isString ? 2 * CountStringChars(token) : 2;

            // Unsized #d
            if (isString)
                return CountStringChars(token); // chars as 16-bit each
            if (TryParseNumber(token, out uint val))
                return val <= 0xFFFF ? 1 : 2;
            // Unknown/symbolic token in #d: assume 1 word (conservative)
            return 1;
        }

        /// <summary>
        /// Formats the display text for the token at the given sub-word index.
        /// For #d16 strings: prints the char at sub-index as "X".
        /// For #d32 strings: prints the char at sub-index/2 only on the first half; blank on the second half.
        /// For numeric tokens spanning 2 words (#d32 or #d with 32-bit): prints hex only on the first word; blank on the second.
        /// </summary>
        private static string FormatDataTokenDisplay(string directive, string token, int subIndex)
        {
            if (IsQuoted(token))
            {
                // String literal: we print the current character as "X".
                // For #d32, each char is 2 words (hi, lo) → print only on the first word.
                int charIndex = directive.Equals(AssemblerConstants.D32Directive, StringComparison.OrdinalIgnoreCase)
                    ? subIndex / 2
                    : subIndex;

                int len = CountStringChars(token);
                if (charIndex < 0 || charIndex >= len) return string.Empty;

                char ch = ExtractStringChar(token, charIndex);
                return $"\"{ch}\"" + (directive.Equals(AssemblerConstants.D32Directive, StringComparison.OrdinalIgnoreCase) && (subIndex % 2 == 1) ? string.Empty : string.Empty);
            }

            // Numeric/symbolic token
            if (TryParseNumber(token, out uint val))
            {
                // Sized rules:
                if (directive.Equals(AssemblerConstants.D16Directive, StringComparison.OrdinalIgnoreCase))
                    return $"0x{val & 0xFFFF:X}";

                if (directive.Equals(AssemblerConstants.D32Directive, StringComparison.OrdinalIgnoreCase))
                    return subIndex == 0 ? $"0x{val:X}" : string.Empty; // show once

                // Unsized #d
                if (val <= 0xFFFF)
                    return $"0x{val:X}";
                else
                    return subIndex == 0 ? $"0x{val:X}" : string.Empty; // 32-bit → show once
            }

            // Fallback for symbolic tokens in data (no substitution): show as-is only on first sub-word if multi-word.
            if (directive.Equals(AssemblerConstants.D32Directive, StringComparison.OrdinalIgnoreCase))
                return subIndex == 0 ? token : string.Empty;

            // #d16 or #d (assuming 1 word) → show token
            return token;
        }

        private static bool IsQuoted(string s) => s.Length >= 2 && s[0] == '"' && s[^1] == '"';
        private static int CountStringChars(string s) => IsQuoted(s) ? s.Length - 2 : 0;
        private static char ExtractStringChar(string s, int idx) => s[idx + 1];

        /// <summary>
        /// Formats the display value for an injected constant word (not backed by a source data directive).
        /// Prefers ASCII for printable characters, decimal for small values, hex otherwise.
        /// </summary>
        private static string FormatConstantLineValue(ushort word)
        {
            if (word is >= 0x0020 and <= 0x007E)
                return $"\"{(char)word}\"";
            return word.ToString();
        }

        /// <summary>
        /// Single row of the annotated table.
        /// </summary>
        private sealed record Row(string Addr, string Value, string Line, string Label, string Source);
    }
}
