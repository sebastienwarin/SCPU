using Microsoft.Extensions.Logging;
using NCalc;
using SCPU.Assembler.Model;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SCPU.Assembler
{
    /// <summary>
    /// Parses S-CPU assembly source files into normalized program lines ready for assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This parser handles the complete preprocessing pipeline:
    /// <list type="number">
    ///   <item><description>Load embedded macro definitions and constants</description></item>
    ///   <item><description>Process file inclusions (#include directives)</description></item>
    ///   <item><description>Parse conditional compilation directives (#if, #else, #endif)</description></item>
    ///   <item><description>Evaluate and substitute constant definitions (#const)</description></item>
    ///   <item><description>Split and normalize inline labels</description></item>
    ///   <item><description>Expand macro invocations recursively</description></item>
    ///   <item><description>Compute hierarchical label scoping</description></item>
    ///   <item><description>Ensure valid program entry point (ENTRY_POINT)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class Parser
    {
        private static readonly List<(Regex Pattern, List<string> Lines)> _macros = [];

        private static readonly Regex _identifierRegex = new(@"\b([A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
        private static readonly Regex _definedFunctionRegex = new(@"\bdefined\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _subLabelReferenceRegex = new(@"(?<=(^|[\s\(\[,=+\-*/%]))\.+[A-Za-z0-9_]+", RegexOptions.Compiled);
        private static readonly Regex _macroArgumentRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

        private readonly ILogger<Parser> _logger;

        private readonly HashSet<string> _includedFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _constants = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Regex> _constantRegexCache = new(StringComparer.Ordinal);

        private AssemblyRequest? _request;
        private string? _currentBank;

        /// <summary>
        /// Initializes a new instance of the <see cref="Parser"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic and error messages during parsing.</param>
        public Parser(ILogger<Parser> logger)
        {
            _logger = logger;
            LoadEmbeddedMacros();
        }

        /// <summary>
        /// Gets a read-only snapshot of all constants defined during parsing.
        /// </summary>
        /// <returns>
        /// Dictionary mapping constant names (case-insensitive) to their evaluated integer values.
        /// </returns>
        public IReadOnlyDictionary<string, int> GetConstants() => _constants;

        /// <summary>
        /// Parses an assembly request into preprocessed and normalized program lines.
        /// </summary>
        /// <param name="request">Assembly request with source and optional compile-time defines.</param>
        /// <param name="injectBootloader">
        /// If <c>true</c>, the assembler's bootloader code is automatically prepended before user code.
        /// Default is <c>true</c>.
        /// </param>
        /// <returns>List of preprocessed <see cref="Line"/> objects ready for assembly.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown for multiple ENTRY_POINT definitions or invalid directives.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown if a source file or #include path cannot be found.
        /// </exception>
        public async Task<List<Line>> ParseAsync(AssemblyRequest request, bool injectBootloader = true)
        {
            // Reset
            _request = request;
            _currentBank = null;
            _includedFiles.Clear();
            _constants.Clear();
            _constantRegexCache.Clear();

            // Loads embedded constants
            var constants = await LoadAssemblySourceFromResource(AssemblerConstants.ConstantsResourceName);
            await ParseRawLines(constants, handleIncludes: false);

            // Add user-provided defines
            if (request.Defines != null)
            {
                foreach (var define in request.Defines)
                {
                    try
                    {
                        if (_constants.ContainsKey(define.Key))
                        {
                            _logger.LogError("Cannot add user-defined constant '{Name}': symbol already defined (reserved constant)", define.Key);
                        }
                        else
                        {
                            _constants.Add(define.Key, EvaluateConstantExpression(define.Value));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to evaluate user-defined constant '{Name}' = '{Expr}'", define.Key, define.Value);
                    }
                }
            }

            _logger.LogInformation("Parsing {Source}", request.Source.Identifier);
            _includedFiles.Add(request.Source.Identifier);

            var preMacroLines = new List<(SourceRange Source, string Content, string Bank)>();

            // 1. Optionally prepend bootloader code
            if (injectBootloader)
            {
                var bootloader = await LoadAssemblySourceFromResource(AssemblerConstants.BootloaderResourceName);
                preMacroLines.AddRange(await ParseRawLines(bootloader, handleIncludes: false));
            }

            // 2. Parse main file (and recursively includes if needed)
            preMacroLines.AddRange(await ParseRawLines(request.Source, handleIncludes: true));

            // 3. Separate banks
            var programLines = preMacroLines.Where(l => l.Bank == AssemblerConstants.ProgramBankName).ToList();
            var userPageLines = preMacroLines.Where(l => l.Bank == AssemblerConstants.UserPageBankName).ToList();

            // 4. Split inline labels
            programLines = [.. SplitInlineLabels(programLines)];

            // 5. Expand all macros recursively for program bank lines
            var expandedMacroLines = ExpandAllMacrosRecursively(programLines);

            // 6. Compute hierarchical labels and sub-labels for each bank
            var allLines = ComputeHierarchicalLabels(expandedMacroLines);
            allLines.AddRange(ComputeHierarchicalLabels(userPageLines));

            // 7. Replace constants
            for (int i = 0; i < allLines.Count; i++)
            {
                var replacedContent = ReplaceConstants(allLines[i].Content);
                if (!string.Equals(replacedContent, allLines[i].Content, StringComparison.Ordinal))
                {
                    allLines[i] = Line.ParseAndCreate(allLines[i].Source, replacedContent, allLines[i].Labels, allLines[i].Bank);
                }
            }

            // 8. Ensure a single ENTRY_POINT in program bank (inject after bootloader if missing)
            var entryPointLines = allLines
                .Where(l => l.Labels.Contains(AssemblerConstants.EntryPointLabel) && l.Bank == AssemblerConstants.ProgramBankName)
                .ToList(); ;
            if (entryPointLines.Count > 1) // Multiple ENTRY_POINT found -> Error
            {   
                _logger.LogError("Multiple ENTRY_POINT found in program bank at: {Locations}.",
                    string.Join(", ", entryPointLines.Select(l => l.Source)));
                throw new InvalidOperationException("Multiple ENTRY_POINT found in program bank");
            }
            else if(entryPointLines.Count == 1) // Exactly one user-defined ENTRY_POINT -> OK
            {
                _logger.LogInformation("ENTRY_POINT found at {Source}", entryPointLines[0].Source);
            }
            else // No ENTRY_POINT -> inject after the bootloader region
            {
                var targetIdx = allLines.FindLastIndex(l =>
                    l.Bank == AssemblerConstants.ProgramBankName &&
                    l.Source.Source.Identifier == AssemblerConstants.BootloaderResourceName) + 1;

                var target = allLines[targetIdx];
                target.Labels.Add(AssemblerConstants.EntryPointLabel);

                _logger.LogInformation("ENTRY_POINT defined automatically at {Source}", target.Source);
            }

            return allLines;
        }

        private async Task<List<(SourceRange Source, string Content, string Bank)>> ParseRawLines(SourceDocument assemblySource, bool handleIncludes)
        {
            _logger.LogDebug("Parsing raw lines from {Source}", assemblySource.Identifier);

            var result = new List<(SourceRange, string, string)>();
            var includePaths = new List<string>();
            var bank = _currentBank ?? AssemblerConstants.ProgramBankName;

            // Normalize line endings and preserve logical lines
            var logicalLines = (await assemblySource.ReadAllTextAsync())
                                        .Split('\n')
                                        .Select(s => s.TrimEnd('\r'))
                                        .ToArray();

            // Apply conditional compilation using the currently known #const values.
            var lines = ConditionalPreprocessor.Filter(logicalLines, assemblySource, _logger, EvaluateConditionalExpression);

            // For each "active" lines
            for (int i = 0; i < lines.Length; i++)
            {
                var rawLine = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith(AssemblerConstants.CommentChar))
                    continue;

                var content = rawLine.Split(AssemblerConstants.CommentChar)[0].Trim();
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                if (content.StartsWith(AssemblerConstants.IncludeDirective) && handleIncludes)
                {
                    _logger.LogDebug("Found #include directive at {Source}:{Line}: {Include}", assemblySource.Identifier, i + 1, content);
                    includePaths.Add(ExtractIncludePath(content));
                    continue;
                }
                else if (content.StartsWith(AssemblerConstants.BankDirective))
                {
                    bank = content.Split(' ', 2)[1].Trim();
                    _logger.LogInformation("Switching to bank '{Bank}' at {Source}:{Line}", bank, assemblySource.Identifier, i + 1);
                    _currentBank = bank;
                    continue;
                }
                else if (content.StartsWith(AssemblerConstants.ConstDirective))
                {
                    var parts = content.Split(' ', 2)[1].Split('=', 2);
                    if (parts.Length != 2)
                    {
                        _logger.LogError("Invalid #const format at {Source}:{Line}: {LineContent}", assemblySource.Identifier, i + 1, content);
                        throw new FormatException($"Invalid #const format at {assemblySource.Identifier}:{i + 1}");
                    }

                    var key = parts[0].Trim();
                    var valueExpr = parts[1].Trim();

                    int value;
                    try
                    {
                        value = EvaluateConstantExpression(valueExpr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to evaluate #const '{Key}' at {File}:{Line}: '{Expr}'", key, assemblySource.Identifier, i + 1, valueExpr);
                        throw;
                    }

                    if (_constants.ContainsKey(key))
                    {
                        switch (_request!.ConflictPolicy)
                        {
                            case DefineConflictPolicy.KeepExisting:
                                _logger.LogWarning("Constant '{Key}' is already defined. Skipping redefinition at {File}:{Line}.", key, assemblySource.Identifier, i + 1);
                                break;

                            case DefineConflictPolicy.Overwrite:
                                var oldValue = _constants[key];
                                _constants[key] = value;
                                _logger.LogWarning("Overwriting constant '{Key}' at {File}:{Line}: {Old} -> {New}.", key, assemblySource.Identifier, i + 1, oldValue, value);
                                break;

                            case DefineConflictPolicy.ErrorOnConflict:
                                _logger.LogError("Redefinition of constant '{Key}' is not allowed (policy ErrorOnConflict) at {File}:{Line}.", key, assemblySource.Identifier, i + 1);
                                throw new InvalidOperationException($"Redefinition of constant '{key}' is not allowed (at {assemblySource.Identifier}:{i + 1}).");
                        }
                    }
                    else
                    {
                        _constants[key] = value;
                        _logger.LogDebug("Added constant: {Key} = {Value}", key, value);
                    }

                    continue;
                }

                result.Add((new SourceRange(assemblySource, i + 1, rawLine), content, bank));
            }

            // Includes are processed AFTER the main lines!
            foreach (var includePath in includePaths)
            {
                var includeFile = new FileInfo(Path.Combine(assemblySource.BaseDirectory!, includePath));
                if (!includeFile.Exists)
                {
                    _logger.LogError("Included file not found: {Include} (from {Source})", includePath, assemblySource.Identifier);
                    throw new FileNotFoundException($"Included file not found: {includePath}", includeFile.FullName);
                }

                if (!_includedFiles.Contains(includeFile.FullName))
                {
                    _logger.LogInformation("Processing included file: {File}", includeFile.FullName);
                    result.AddRange(await ParseRawLines(SourceDocument.FromFile(includeFile), handleIncludes: true));
                }
                else
                {
                    _logger.LogDebug("Included file {File} already loaded, skipping", includeFile.FullName);
                }
            }

            return result;
        }

        private static IEnumerable<(SourceRange Source, string Content, string Bank)> SplitInlineLabels(IEnumerable<(SourceRange Source, string Content, string Bank)> input)
        {
            foreach (var (src, content, bank) in input)
            {
                string rest = content;
                int colonIdx;

                // Unstack all leading labels at the beginning of the line
                while (true)
                {
                    colonIdx = rest.IndexOf(':');
                    if (colonIdx >= 0 &&
                        rest[..colonIdx].All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.') &&
                        (colonIdx == 0 || !char.IsWhiteSpace(rest[colonIdx - 1])))
                    {
                        var labelPart = rest[..colonIdx].Trim();
                        yield return (src, labelPart + ":", bank); // Yield the label as a standalone line
                        rest = rest[(colonIdx + 1)..].Trim();
                        continue;
                    }
                    break;
                }
                if (!string.IsNullOrWhiteSpace(rest))
                    yield return (src, rest, bank);
            }
        }

        private List<(SourceRange, string, string)> ExpandAllMacrosRecursively(List<(SourceRange Source, string Content, string Bank)> inputLines)
        {
            _logger.LogDebug("Expanding macros recursively for {LineCount} lines", inputLines.Count);

            bool changed;
            var linesToExpand = inputLines;
            int macroExpansions = 0;

            do
            {
                changed = false;
                var next = new List<(SourceRange, string, string)>();
                foreach (var (source, content, bank) in linesToExpand)
                {
                    bool expanded = false;
                    foreach (var macro in _macros)
                    {
                        var match = macro.Pattern.Match(content);
                        if (match.Success)
                        {
                            changed = expanded = true;
                            macroExpansions++;
                            string uid = Guid.NewGuid().ToString("N")[..6];
                            _logger.LogDebug("Expanding macro for line {File}:{Line}: {Content}", source.Source.Identifier, source.Line, content);
                            foreach (var exp in macro.Lines)
                            {
                                var replaced = exp.Replace("{uid}", uid);
                                foreach (var groupName in macro.Pattern.GetGroupNames().Where(g => g != "0"))
                                    replaced = replaced.Replace($"{{{groupName}}}", match.Groups[groupName].Value);
                                next.Add((source, replaced, bank));
                            }
                            break;
                        }
                    }
                    if (!expanded)
                        next.Add((source, content, bank));
                }
                linesToExpand = next;
            } while (changed);

            if (macroExpansions > 0)
                _logger.LogInformation("{Count} macro expansion(s) performed", macroExpansions);

            return linesToExpand;
        }

        private List<Line> ComputeHierarchicalLabels(List<(SourceRange Source, string Content, string Bank)> macroLines)
        {
            var lines = new List<Line>();
            var labelStack = new List<string>();
            var pendingLabels = new List<string>();

            foreach (var (source, lineContentIn, bank) in macroLines)
            {
                string lineContent = lineContentIn.Trim();
                bool isPrg = bank == AssemblerConstants.ProgramBankName;
                bool isData = lineContent.StartsWith(AssemblerConstants.ResDirective) || lineContent.StartsWith(AssemblerConstants.DataDirective);

                // Support several consecutive labels, e.g. ".foo: .bar: instr"
                while (true)
                {
                    int colonIdx = lineContent.IndexOf(':');
                    if (colonIdx >= 0
                        && lineContent[..colonIdx].All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.')
                        && (colonIdx == 0 || !char.IsWhiteSpace(lineContent[colonIdx - 1])))
                    {
                        var labelPart = lineContent[..colonIdx].Trim();
                        lineContent = lineContent[(colonIdx + 1)..].Trim();

                        int dotCount = labelPart.TakeWhile(c => c == '.').Count();
                        var labelName = labelPart.TrimStart('.');

                        bool isTransparent = labelName.StartsWith("__");

                        if (isPrg)
                        {
                            if (!isTransparent)
                            {
                                if (dotCount == 0)
                                {
                                    labelStack.Clear();
                                    labelStack.Add(labelName);
                                }
                                else
                                {
                                    if (dotCount > labelStack.Count)
                                    {
                                        _logger.LogError("Too many dots in label at {File}:{Line}", source.Source.Identifier, source.Line);
                                        throw new InvalidOperationException($"Too many dots at {source.Source.Identifier}:{source.Line}");
                                    }
                                    labelStack = [.. labelStack.Take(dotCount)];
                                    labelStack.Add(labelName);
                                }
                            }
                            pendingLabels.Add(isTransparent ? labelName : string.Join('.', labelStack));
                        }
                        else
                        {
                            labelStack.Clear();
                            labelStack.Add(labelPart);
                            pendingLabels.Add(labelPart);
                        }

                        // Continue if another label is defined immediately after (e.g. ".foo: .bar: ...")
                        continue;
                    }
                    break;
                }

                // If the line only has one or more labels, and nothing else, skip (nothing to emit)
                if (string.IsNullOrWhiteSpace(lineContent))
                    continue;

                var labelsForLine = pendingLabels.ToList();
                pendingLabels.Clear();

                if (labelsForLine.Count == 0 && isData)
                {
                    _logger.LogError("Directive {Directive} requires a label at {File}:{Line}", lineContent, source.Source.Identifier, source.Line);
                    throw new InvalidOperationException($"Directive {lineContent} requires a label at {source.Source.Identifier}:{source.Line}");
                }

                if (isPrg && !isData)
                {
                    // Replace short sub-label references in operands with their fully qualified names
                    lineContent = _subLabelReferenceRegex.Replace(lineContent, m =>
                    {
                        int dots = m.Value.TakeWhile(c => c == '.').Count();
                        var name = m.Value[dots..];
                        if (dots == 0) return name;
                        if (labelStack.Count < dots)
                        {
                            _logger.LogError("Too many dots in operand at {File}:{Line}", source.Source.Identifier, source.Line);
                            throw new InvalidOperationException($"Too many dots in operand at {source.Source.Identifier}:{source.Line}");
                        }
                        var parent = labelStack.Take(dots).ToList();
                        parent.Add(name);
                        return string.Join('.', parent);
                    });
                }

                lines.Add(Line.ParseAndCreate(source, lineContent, labelsForLine, bank));
            }

            _logger.LogDebug("Computed {LineCount} hierarchical label lines", lines.Count);
            return lines;
        }

        private int EvaluateConstantExpression(string expression)
        {
            // Replace constants
            expression = ReplaceConstants(expression);

            // Rewrites numeric literals
            expression = AssemblyStringUtils.ReplaceNumericLiterals(expression);

            // Evaluate expression
            try
            {
                var expr = new Expression(expression);
                var result = expr.Evaluate();
                _logger.LogDebug("Evaluated constant expression: {Expr} = {Value}", expression, result);
                return Convert.ToInt32(result);
            }
            catch
            {
                _logger.LogError("Invalid constant expression: {Expr}", expression);
                throw new InvalidOperationException($"Invalid constant expression: {expression}");
            }
        }

        private string ReplaceConstants(string content)
        {
            // 1. Quick check if ANY constant exists in the content
            bool hasAnyConstant = false;
            foreach (var kvp in _constants)
            {
                if (content.Contains(kvp.Key, StringComparison.Ordinal))
                {
                    hasAnyConstant = true;
                    break;
                }
            }
            if (!hasAnyConstant)
                return content;

            // 2. Process longest constant names first (avoid partial matches)
            var sortedConstants = _constants.OrderByDescending(k => k.Key.Length).ToList();
            foreach (var kvp in sortedConstants)
            {
                // Compile and cache regex patterns
                string patternKey = $"\\b{Regex.Escape(kvp.Key)}\\b";
                if (!_constantRegexCache.TryGetValue(patternKey, out var regex))
                {
                    regex = new Regex(patternKey, RegexOptions.Compiled);
                    _constantRegexCache[patternKey] = regex;
                }
                content = regex.Replace(content, kvp.Value.ToString());
            }

            return content;
        }

        private bool EvaluateConditionalExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new FormatException("Empty conditional expression.");
            }
            expression = expression.Trim();

            // defined(NAME) : true if defined otherwise false
            expression = _definedFunctionRegex.Replace(expression, match =>
            {
                var name = match.Groups[1].Value;
                return _constants.ContainsKey(name) ? bool.TrueString : bool.FalseString;
            });

            // Replace identifiers with #const values, or false if unknown
            expression = _identifierRegex.Replace(expression, match =>
            {
                var id = match.Value;

                // Quick check: if it's a boolean literal, keep it
                if (id == bool.TrueString || id == bool.FalseString)
                    return id;

                // Check if it's a defined constant
                if (_constants.TryGetValue(id, out var val))
                    return val.ToString();

                // If not defined, return false (to allow short-circuit logic)
                _logger.LogDebug("Conditional uses undefined identifier '{Id}', treating as false", id);
                return bool.FalseString;
            });

            // Rewrites numeric literals
            expression = AssemblyStringUtils.ReplaceNumericLiterals(expression);

            // Evaluate expression
            try
            {
                var expr = new Expression(expression);
                var result = expr.Evaluate();

                if (result is bool b)
                    return b;

                if (result is IConvertible c)
                    return Math.Abs(c.ToDouble(null)) > double.Epsilon;

                throw new InvalidOperationException("Conditional did not evaluate to boolean/numeric.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate conditional expression: {Expression}", expression);
                throw new InvalidOperationException($"Conditional did not evaluate: {expression}", ex);
            }
        }

        private void LoadEmbeddedMacros()
        {
            if (_macros.Count == 0)
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resources = assembly.GetManifestResourceNames()
                    .Where(n => n.StartsWith(AssemblerConstants.MacrosResourcePrefix) && n.EndsWith(AssemblerConstants.MacroResourceExtension))
                    .ToList();

                foreach (var resName in resources)
                {
                    using var stream = assembly.GetManifestResourceStream(resName)!;
                    using var reader = new StreamReader(stream);

                    string? line;
                    string? currentMacro = null;
                    var macroLines = new List<string>();

                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith(AssemblerConstants.CommentChar)) continue;
                        line = line.Split(AssemblerConstants.CommentChar)[0].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        if (line.StartsWith(AssemblerConstants.MacroPatternStart))
                        {
                            if (currentMacro != null)
                            {
                                var regex = new Regex(BuildMacroRegexFromTemplate(currentMacro), RegexOptions.IgnoreCase);
                                _macros.Add((regex, [.. macroLines]));
                                _logger.LogDebug("Loaded macro: {Macro}", currentMacro);
                            }
                            currentMacro = line[7..^1];
                            macroLines.Clear();
                        }
                        else if (currentMacro != null)
                        {
                            macroLines.Add(line);
                        }
                    }

                    if (currentMacro != null)
                    {
                        var regex = new Regex(BuildMacroRegexFromTemplate(currentMacro), RegexOptions.IgnoreCase);
                        _macros.Add((regex, [.. macroLines]));
                        _logger.LogDebug("Loaded macro: {Macro}", currentMacro);
                    }
                }

                _logger.LogInformation("Loaded {MacroCount} embedded macro(s)", _macros.Count);
            }
        }

        private static async Task<SourceDocument> LoadAssemblySourceFromResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
            using var reader = new StreamReader(stream);
            return SourceDocument.FromInline(await reader.ReadToEndAsync(), resourceName);
        }

        private static string BuildMacroRegexFromTemplate(string macroKey)
        {
            var matches = _macroArgumentRegex.Matches(macroKey);
            var regex = new StringBuilder("^");
            int lastIndex = 0;

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (match.Index > lastIndex)
                {
                    var literal = macroKey[lastIndex..match.Index];
                    regex.Append(Regex.Escape(literal).Replace(@"\ ", @"\s+"));
                }

                var name = match.Groups[1].Value;
                if (i < matches.Count - 1)
                {
                    regex.Append($@"(?<{name}>[^,]+?)");
                }
                else
                {
                    regex.Append($@"(?<{name}>[^,]+)");
                }
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < macroKey.Length)
            {
                var literal = macroKey[lastIndex..];
                regex.Append(Regex.Escape(literal).Replace(@"\ ", @"\s+"));
            }

            regex.Append('$');
            return regex.ToString();
        }

        /// <summary>
        /// Extracts file path from a #include directive.
        /// </summary>
        /// <remarks>
        /// Expects format: #include "path/to/file.asm"
        /// </remarks>
        /// <param name="line">The #include directive line.</param>
        /// <returns>Extracted file path (without quotes).</returns>
        /// <exception cref="FormatException">Thrown for malformed #include syntax.</exception>
        private static string ExtractIncludePath(string line)
        {
            var start = line.IndexOf('"');
            var end = line.LastIndexOf('"');
            if (start < 0 || end <= start)
                throw new FormatException($"Invalid #include syntax: {line}");

            return line[(start + 1)..end];
        }
    }
}
