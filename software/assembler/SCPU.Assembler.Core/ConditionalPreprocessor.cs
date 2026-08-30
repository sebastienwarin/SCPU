using Microsoft.Extensions.Logging;
using SCPU.Assembler.Model;

namespace SCPU.Assembler
{
    /// <summary>
    /// Applies conditional compilation using <c>#if</c>, <c>#elif</c>, and <c>#else</c> directives
    /// with brace-delimited blocks.
    /// This preprocessor keeps only the lines from the selected branch and leaves all other lines intact.
    /// It relies on the supplied <paramref name="evaluate"/> callback to interpret condition expressions.
    /// </summary>
    internal static class ConditionalPreprocessor
    {
        /// <summary>
        /// Filters the given lines by evaluating conditional directives and returning only active lines.
        /// Non-conditional lines are passed through unchanged.
        /// </summary>
        /// <param name="lines">Raw logical lines (one string per physical line; not altered).</param>
        /// <param name="source">Current source (used for diagnostics).</param>
        /// <param name="logger">Logger for diagnostic messages.</param>
        /// <param name="evaluate">Boolean expression evaluator used for <c>#if</c>/<c>#elif</c> conditions.</param>
        /// <returns>An array of lines with all inactive conditional branches removed.</returns>
        public static string[] Filter(string[] lines, SourceDocument source, ILogger logger, Func<string, bool> evaluate)
        {
            var output = new List<string>();
            int i = 0;

            while (i < lines.Length)
            {
                string raw = lines[i];
                string trimmed = raw.TrimStart();

                // Only detect directive at line start (ignoring leading whitespace)
                if (trimmed.StartsWith(AssemblerConstants.IfDirective, StringComparison.OrdinalIgnoreCase))
                {
                    var chosen = ConsumeIfElifElseGroup(lines, ref i, source, logger, evaluate);
                    output.AddRange(chosen);
                    continue;
                }

                output.Add(raw);
                i++;
            }

            return output.ToArray();
        }

        /// <summary>
        /// Consumes a full conditional group starting at a <c>#if</c> line:
        /// <c>#if &lt;expr&gt; { ... } [#elif &lt;expr&gt; { ... }]* [#else { ... }]</c>
        /// Returns the selected branch (no recursion; nesting is not supported).
        /// </summary>
        private static string[] ConsumeIfElifElseGroup(string[] lines, ref int i, SourceDocument source, ILogger logger, Func<string, bool> evaluate)
        {
            var branches = new List<(bool? Cond, List<string> Block)>();

            // --- #if (required)
            var (ifExpr, ifOpenBraceLine) = ParseHeaderAndFindOpeningBrace(lines, i, AssemblerConstants.IfDirective, source, expectExpr: true);
            var ifBlock = ReadSingleLevelBlock(lines, ifOpenBraceLine, source);
            i = ifBlock.NextIndex;
            bool ifVal = EvaluateBool(ifExpr, source, ifOpenBraceLine, logger, evaluate);
            branches.Add((ifVal, ifBlock.Lines));

            // --- #elif (zero or more)
            while (i < lines.Length)
            {
                string probe = lines[i].TrimStart();
                if (!probe.StartsWith(AssemblerConstants.ElifDirective, StringComparison.OrdinalIgnoreCase))
                    break;

                var (elifExpr, elifOpenBraceLine) = ParseHeaderAndFindOpeningBrace(lines, i, AssemblerConstants.ElifDirective, source, expectExpr: true);
                var elifBlock = ReadSingleLevelBlock(lines, elifOpenBraceLine, source);
                i = elifBlock.NextIndex;
                bool elifVal = EvaluateBool(elifExpr, source, elifOpenBraceLine, logger, evaluate);
                branches.Add((elifVal, elifBlock.Lines));
            }

            // --- #else (optional)
            if (i < lines.Length)
            {
                string probe = lines[i].TrimStart();
                if (probe.StartsWith(AssemblerConstants.ElseDirective, StringComparison.OrdinalIgnoreCase))
                {
                    var (_, elseOpenBraceLine) = ParseHeaderAndFindOpeningBrace(lines, i, AssemblerConstants.ElseDirective, source, expectExpr: false);
                    var elseBlock = ReadSingleLevelBlock(lines, elseOpenBraceLine, source);
                    i = elseBlock.NextIndex;
                    branches.Add((null, elseBlock.Lines));
                }
            }

            // Pick first true branch; otherwise take #else if present.
            foreach (var (cond, block) in branches)
            {
                if (cond == true)
                {
                    return [.. block];
                }
            }

            var elseBranch = branches.LastOrDefault(b => b.Cond == null);
            return elseBranch.Block.ToArray() ?? [];
        }

        /// <summary>
        /// Parses a directive header (<c>#if</c> or <c>#elif</c> or <c>#else</c>) on the current line,
        /// extracts the condition expression when applicable, and finds the line that contains the opening <c>{</c>.
        /// The opening brace can be on the same line as the directive or on the next non-empty line.
        /// </summary>
        private static (string Expr, int OpenBraceLine) ParseHeaderAndFindOpeningBrace(string[] lines, int directiveLine, string directive, SourceDocument source, bool expectExpr)
        {
            string header = lines[directiveLine];
            string headerTrim = header.TrimStart();

            if (!headerTrim.StartsWith(directive, StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"{directive} expected at {source.Identifier}:{directiveLine + 1}");

            // Extract expression (only from the directive line).
            string expr = string.Empty;
            if (expectExpr)
            {
                expr = headerTrim.Substring(directive.Length).Trim();
                // If '{' is on the same line, ignore anything after it for the expression.
                int braceIdx = expr.IndexOf('{');
                if (braceIdx >= 0)
                    expr = expr[..braceIdx].Trim();

                if (expr.Length == 0)
                    throw new FormatException($"Missing condition after {directive} at {source.Identifier}:{directiveLine + 1}");
            }

            // Find the opening brace "{" either on the same line or on the next non-empty line.
            if (headerTrim.Contains('{'))
                return (expr, directiveLine);

            int idx = directiveLine + 1;
            while (idx < lines.Length)
            {
                string candidate = lines[idx].TrimStart();
                if (candidate.Length == 0) { idx++; continue; } // skip empty lines
                if (candidate.StartsWith("{"))
                    return (expr, idx);

                throw new FormatException($"'{{' expected after {directive} at {source.Identifier}:{directiveLine + 1}");
            }

            throw new FormatException($"'{{' not found for {directive} at {source.Identifier}:{directiveLine + 1}");
        }

        /// <summary>
        /// Reads a non-nested brace-delimited block that starts at <paramref name="openBraceLine"/>.
        /// Assumes the opening brace is on that line. Returns the block lines (without the opening and closing braces)
        /// and the index of the first line after the closing brace.
        /// </summary>
        private static (List<string> Lines, int NextIndex) ReadSingleLevelBlock(string[] lines, int openBraceLine, SourceDocument source)
        {
            var collected = new List<string>();

            // Skip the opening brace line, then read until the next line that contains a closing brace '}'.
            for (int j = openBraceLine + 1; j < lines.Length; j++)
            {
                string raw = lines[j];
                string t = raw.TrimStart();

                // First closing brace ends the block (nesting is not supported in this preprocessor).
                if (t.Contains('}'))
                    return (collected, j + 1);

                collected.Add(raw);
            }

            throw new FormatException($"Unclosed '{{' block starting at {source.Identifier}:{openBraceLine + 1}");
        }

        /// <summary>
        /// Wraps user expression evaluation with error reporting (adds file/line context).
        /// </summary>
        private static bool EvaluateBool(string expr, SourceDocument source, int lineIndex, ILogger logger, Func<string, bool> evaluate)
        {
            try
            {
                return evaluate(expr);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invalid conditional expression '{Expr}' at {Source}:{Line}", expr, source.Identifier, lineIndex + 1);
                throw new InvalidOperationException($"Invalid conditional expression at {source.Identifier}:{lineIndex + 1}: {expr}");
            }
        }
    }
}
