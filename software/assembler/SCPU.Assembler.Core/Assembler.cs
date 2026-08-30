using Microsoft.Extensions.Logging;
using NCalc;
using SCPU.Architecture;
using SCPU.Assembler.Model;
using System.Text.RegularExpressions;

namespace SCPU.Assembler
{
    /// <summary>
    /// Coordinates the S-CPU assembly pipeline, delegating source preprocessing to <see cref="Parser"/>
    /// and performing core assembly passes to produce an <see cref="AssemblyResult"/>.
    /// </summary>
    /// <remarks>
    /// Pure in-memory assembly — no files are written. To persist results, use an <c>IAssemblyExporter</c>
    /// after calling <see cref="AssembleAsync(AssemblyRequest)"/>.
    /// </remarks>
    public class Assembler(Parser parser, ILogger<Assembler> logger)
    {
        private static readonly char[] _expressionOperators = ['+', '-', '*', '/', '%', '(', ')', '&', '|', '^', '<', '>', '='];
        private static readonly Regex _programCounterRegex =
            new($"\\{AssemblerConstants.ProgramCounterSymbol}", RegexOptions.Compiled);

        /// <summary>
        /// Assembles the specified S-CPU source code into machine words and binary output.
        /// </summary>
        /// <param name="request">
        /// Assembly request containing the source code and optional per-call settings (e.g., pre-defined constants).
        /// </param>
        /// <returns>An <see cref="AssemblyResult"/> containing the binary output, annotated words, resolved labels, and constants.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
        /// <exception cref="FileNotFoundException">Thrown when source files or #include paths cannot be found.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown for assembly errors: duplicate labels, out-of-range addresses, malformed expressions.
        /// </exception>
        public async Task<AssemblyResult> AssembleAsync(AssemblyRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var lines = await parser.ParseAsync(request);
                logger.LogDebug("Parsed {LineCount} lines from {Source}", lines.Count, request.Source.Identifier);

                var result = AssembleCore(lines);

                logger.LogInformation("Assembly completed successfully. Output size: {ByteCount} bytes", result.Binary.Length);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Assembly failed for source: {Source}", request.Source.Identifier);
                throw;
            }
        }

        private AssemblyResult AssembleCore(List<Line> lines)
        {
            logger.LogInformation("Starting core assembly pipeline with {LineCount} input lines", lines.Count);

            logger.LogInformation("Stage 1: Allocating label addresses");
            var labelAddresses = AllocateLabels(lines);

            logger.LogInformation("Stage 2: Computing ROM addresses");
            var lineToRomAddress = ComputeRomAddresses(lines, labelAddresses);

            logger.LogInformation("Stage 3: Patching instructions with fixed-point analysis");
            var (patchedLines, extraData) = PatchInstructionsWithConstants(lines, labelAddresses, lineToRomAddress);

            var bootloaderEndAddress = ComputeBootloaderEndAddress(lines, lineToRomAddress);

            if (extraData.Count > 0)
            {
                logger.LogInformation("Stage 4: Shifting {ConstantCount} ROM label(s)", extraData.Count);
                ShiftLabelsForExtraData(labelAddresses, extraData, bootloaderEndAddress);
            }

            logger.LogInformation("Stage 5: Emitting final machine words");
            var finalWords = EmitFinalWords(patchedLines, extraData, labelAddresses, bootloaderEndAddress);

            logger.LogInformation("Stage 6: Flattening to binary");
            var binaryData = finalWords
                .Select(w => w.Word)
                .SelectMany(w => new[] { (byte)(w >> 8), (byte)(w & 0xFF) })
                .ToArray();

            logger.LogInformation("Assembly complete. Output size: {ByteCount} bytes", binaryData.Length);
            return new AssemblyResult
            {
                Binary = binaryData,
                Constants = new Dictionary<string, int>(parser.GetConstants()),
                FinalWords = finalWords,
                Labels = new Dictionary<string, uint>(labelAddresses)
            };
        }

        private Dictionary<string, uint> AllocateLabels(List<Line> lines)
        {
            var labelAddresses = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            uint userPageAddress = MemoryMap.UserPage.Start;
            ushort romAddress = 0;

            foreach (var line in lines)
            {
                // Assign addresses to labels on this line
                if (line.Labels != null && line.Labels.Count > 0)
                {
                    foreach (var label in line.Labels)
                    {
                        if (labelAddresses.ContainsKey(label))
                        {
                            logger.LogError("Duplicate label '{Label}' at line {LineNumber}", label, line.Source.Line);
                            throw new InvalidOperationException($"Duplicate label: {label}");
                        }

                        labelAddresses[label] = line.Bank == AssemblerConstants.UserPageBankName
                            ? userPageAddress
                            : (uint)romAddress;
                    }

                    // For RAM #res directives, advance userPageAddress
                    if (line.Bank == AssemblerConstants.UserPageBankName &&
                        line.Type == LineType.ReservationDirective &&
                        line.ReservationDirective.HasValue)
                    {
                        uint size = ResolveValue(line.ReservationDirective.Value.SizeExpression, 0, labelAddresses);
                        userPageAddress += size;
                        logger.LogDebug("Reserved {Size} bytes at userpage for label(s): {Labels}", size, string.Join(", ", line.Labels));
                    }
                }

                // For data directives, advance ROM address by number of words generated
                if (line.Type == LineType.DataDirective && line.DataDirective.HasValue)
                {
                    var words = EncodeInstruction(line, romAddress, labelAddresses);
                    romAddress += (ushort)(words.Count() - 1);
                }

                // Increment ROM address for each instruction/data line in program bank
                if (line.Bank == AssemblerConstants.ProgramBankName)
                {
                    romAddress++;
                }
            }

            logger.LogInformation("Allocated {Count} label(s)", labelAddresses.Count);
            return labelAddresses;
        }

        private Dictionary<Line, ushort> ComputeRomAddresses(
            List<Line> lines,
            Dictionary<string, uint> labelAddresses)
        {
            var lineToRomAddress = new Dictionary<Line, ushort>();
            ushort romAddress = 0;

            foreach (var line in lines)
            {
                if (line.Bank != AssemblerConstants.ProgramBankName)
                    continue;

                lineToRomAddress[line] = romAddress;

                // Advance address based on word count (data directives may generate multiple words)
                if (line.Type == LineType.DataDirective && line.DataDirective.HasValue)
                {
                    var words = EncodeDataDirective(
                        line.DataDirective.Value, romAddress, labelAddresses, resolveValue: false);
                    romAddress += (ushort)words.Count;
                }
                else
                {
                    // Instructions generate exactly 1 word
                    romAddress += 1;
                }
            }

            return lineToRomAddress;
        }

        private List<OperandNeedsConstantAnalysis> AnalyzeOperandsForConstantThresholds(
            List<Line> lines,
            Dictionary<string, uint> labelAddresses,
            Dictionary<Line, ushort> lineToRomAddress)
        {
            var operandAnalyses = new List<OperandNeedsConstantAnalysis>();

            foreach (var line in lines)
            {
                if (line.Bank != AssemblerConstants.ProgramBankName ||
                    line.Type != LineType.Instruction ||
                    !line.Instruction.HasValue)
                    continue;

                var instr = line.Instruction.Value;
                ushort pc = lineToRomAddress.TryGetValue(line, out var addr) ? addr : (ushort)0;

                try
                {
                    uint resolvedValue = ResolveValue(instr.Operand, pc, labelAddresses);
                    AddressingMode mode = GetAddressingMode(instr.Operand, resolvedValue);

                    if (!Addressing.TryTranslateVirtualAddress(
                            resolvedValue,
                            Addressing.AddressView.EncodedOperand,
                            out var normalizedValue,
                            out _))
                    {
                        throw new InvalidOperationException($"Unable to map operand '{instr.Operand}' to physical address");
                    }

                    operandAnalyses.Add(new OperandNeedsConstantAnalysis(
                        NormalizedValue: normalizedValue,
                        Mode: mode,
                        IsJump: instr.IsJump,
                        Operand: instr.Operand,
                        ProgramCounter: pc));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to analyze operand '{Operand}' at line {LineNumber}", instr.Operand, line.Source.Line);
                    throw;
                }
            }

            return operandAnalyses;
        }

        private (List<Line> patchedLines, List<(string Label, string Operand, uint Origin, uint Value)> extraData)
            PatchInstructionsWithConstants(
                List<Line> lines,
                Dictionary<string, uint> labelAddresses,
                Dictionary<Line, ushort> lineToRomAddress)
        {
            var operandAnalyses = AnalyzeOperandsForConstantThresholds(lines, labelAddresses, lineToRomAddress);

            if (operandAnalyses.Count == 0)
                return (new List<Line>(lines), new List<(string Label, string Operand, uint Origin, uint Value)>());

            var (finiteThresholds, alwaysConstantIndices) = CalculateThresholds(operandAnalyses);
            int fixedPointN = FindFixedPointN(finiteThresholds, alwaysConstantIndices.Count);
            logger.LogInformation("Fixed-point analysis: {ConstantCount} constant(s) required", fixedPointN);

            var operandNeedsConstant = new bool[operandAnalyses.Count];
            foreach (var idx in alwaysConstantIndices)
                operandNeedsConstant[idx] = true;

            foreach (var (ti, operandIndex) in finiteThresholds)
            {
                if (ti < fixedPointN)
                    operandNeedsConstant[operandIndex] = true;
                else
                    break;
            }

            return GeneratePatchedLinesAndExtraData(lines, labelAddresses, operandAnalyses, operandNeedsConstant);
        }

        private (List<Line> patchedLines, List<(string Label, string Operand, uint Origin, uint Value)> extraData)
            GeneratePatchedLinesAndExtraData(
                List<Line> lines,
                Dictionary<string, uint> labelAddresses,
                List<OperandNeedsConstantAnalysis> operandAnalyses,
                bool[] operandNeedsConstant)
        {
            var patchedLines = new List<Line>();
            var extraData = new List<(string Label, string Operand, uint Origin, uint Value)>();
            var valueToLabel = new Dictionary<ushort, string>();

            int analysisIndex = 0;

            foreach (var line in lines)
            {
                var patchedLine = line;

                if (line.Bank == AssemblerConstants.ProgramBankName &&
                    line.Type == LineType.Instruction &&
                    line.Instruction.HasValue)
                {
                    var instr = line.Instruction.Value;
                    var analysis = operandAnalyses[analysisIndex];
                    bool needsConst = operandNeedsConstant[analysisIndex];

                    string newOperand = instr.Operand;

                    if (needsConst)
                    {
                        ushort value16 = (ushort)analysis.NormalizedValue;
                        string constLabel;

                        if (valueToLabel.TryGetValue(value16, out var existingLabel))
                        {
                            logger.LogDebug("Reusing constant '{Label}' for value 0x{Value:X4}", existingLabel, value16);
                            constLabel = existingLabel;
                        }
                        else
                        {
                            constLabel = $"const_{extraData.Count}";
                            extraData.Add((constLabel, analysis.Operand, analysis.ProgramCounter, analysis.NormalizedValue));
                            valueToLabel[value16] = constLabel;
                            labelAddresses[constLabel] = 0;
                            logger.LogDebug("Created constant label '{Label}' for value 0x{Value:X4}", constLabel, value16);
                        }

                        newOperand = constLabel;
                    }
                    else if (analysis.IsJump)
                    {
                        if (analysis.Mode == AddressingMode.Indirect)
                        {
                            newOperand = instr.Operand.TrimStart(AssemblerConstants.IndirectPrefix);
                        }
                        else
                        {
                            newOperand = $"{AssemblerConstants.ImmediatePrefix}{instr.Operand}";
                        }
                    }

                    if (!ReferenceEquals(newOperand, instr.Operand))
                    {
                        patchedLine = Line.ParseAndCreate(
                            line.Source,
                            line.Content.Replace(instr.Operand, newOperand),
                            line.Labels,
                            line.Bank);
                    }

                    analysisIndex++;
                }

                patchedLines.Add(patchedLine);
            }

            logger.LogInformation("Patching completed: {ConstantCount} constant(s) generated", extraData.Count);
            return (patchedLines, extraData);
        }

        private void ShiftLabelsForExtraData(
            Dictionary<string, uint> labelAddresses,
            List<(string Label, string Operand, uint Origin, uint Value)> extraData,
            uint bootloaderEndAddress)
        {
            if (extraData.Count == 0)
                return;

            uint shiftAmount = (uint)extraData.Count;

            foreach (var kvp in labelAddresses.ToList())
            {
                if (kvp.Value >= bootloaderEndAddress && kvp.Value < MemoryMap.Rom.EndExclusive)
                {
                    uint newAddress = kvp.Value + shiftAmount;
                    if (newAddress > MemoryMap.Rom.EndInclusive)
                    {
                        logger.LogError(
                            "Label '{Label}' shifted from 0x{OldAddr:X4} to 0x{NewAddr:X4} exceeds ROM limit 0x{MaxAddr:X4}",
                            kvp.Key, kvp.Value, newAddress, MemoryMap.Rom.EndInclusive);
                        throw new InvalidOperationException($"Label '{kvp.Key}' exceeds ROM capacity after constant insertion");
                    }
                    labelAddresses[kvp.Key] = newAddress;
                }
            }

            for (int i = 0; i < extraData.Count; i++)
            {
                labelAddresses[extraData[i].Label] = bootloaderEndAddress + (uint)i;
                logger.LogDebug("Constant '{Label}' assigned address 0x{Addr:X4}", extraData[i].Label, bootloaderEndAddress + i);
            }

            logger.LogInformation("Shifted {Count} label(s) by {Amount} word(s) after bootloader at 0x{BootAddr:X4}",
                labelAddresses.Count(kv => kv.Value >= bootloaderEndAddress), shiftAmount, bootloaderEndAddress);
        }

        private List<(object Source, ushort Word)> EmitFinalWords(
            List<Line> patchedLines,
            List<(string Label, string Operand, uint Origin, uint Value)> extraData,
            Dictionary<string, uint> labelAddresses,
            uint bootloaderEndAddress)
        {
            var finalWords = new List<(object Source, ushort Word)>();
            ushort pc = 0;

            for (int i = 0; i < patchedLines.Count; i++)
            {
                var line = patchedLines[i];

                // Insert constants at ENTRY_POINT
                if (i == bootloaderEndAddress && extraData.Count > 0)
                {
                    foreach (var (label, operand, origin, value) in extraData)
                    {
                        // Re-resolve operand value with updated label addresses
                        int pcOrigin = (int)origin + extraData.Count;
                        uint resolvedValue = ResolveValue(operand, (ushort)pcOrigin, labelAddresses);

                        if (!Addressing.TryTranslateVirtualAddress(
                                resolvedValue,
                                Addressing.AddressView.EncodedOperand,
                                out var normalizedValue,
                                out _))
                        {
                            throw new InvalidOperationException($"Cannot resolve constant value for '{label}'");
                        }

                        finalWords.Add((label, normalizedValue));
                        pc++;
                    }
                }

                // Emit program instructions
                if (line.Bank == AssemblerConstants.ProgramBankName)
                {
                    foreach (var word in EncodeInstruction(line, pc, labelAddresses))
                    {
                        finalWords.Add((line, word));
                        pc++;
                    }
                }
            }

            logger.LogInformation("Emitted {Count} machine word(s)", finalWords.Count);
            return finalWords;
        }

        private IEnumerable<ushort> EncodeInstruction(
            Line line,
            uint programCounter,
            IReadOnlyDictionary<string, uint> labelAddresses)
        {
            if (line.Type == LineType.DataDirective && line.DataDirective.HasValue)
            {
                foreach (var word in EncodeDataDirective(line.DataDirective.Value, programCounter, labelAddresses))
                    yield return word;
                yield break;
            }

            if (line.Type != LineType.Instruction || !line.Instruction.HasValue)
            {
                logger.LogError("Invalid line content: {Content}", line.Content);
                throw new InvalidOperationException($"Invalid instruction line: {line.Content}");
            }

            var instr = line.Instruction.Value;
            uint resolvedValue = ResolveValue(instr.Operand, programCounter, labelAddresses);
            AddressingMode mode = GetAddressingMode(instr.Operand, resolvedValue);

            // Instruction-specific restriction(s)
            if (instr.Instruction == Instruction.STA && mode == AddressingMode.Immediate)
                throw new InvalidOperationException($"Instruction STA does not support immediate addressing mode (line {line.Source.Line})");

            if (!Addressing.TryTranslateVirtualAddress(
                    resolvedValue,
                    Addressing.AddressView.EncodedOperand,
                    out var normalizedValue,
                    out _))
            {
                throw new InvalidOperationException($"Cannot map operand '{instr.Operand}' to physical address (line {line.Source.Line})");
            }

            // Encode addressing mode bits and value
            ushort addressBits = mode switch
            {
                // Immediate: must fit in 11 bits
                AddressingMode.Immediate =>
                    EncodeImmediate(normalizedValue, line),

                // Indirect: resolved (virtual) must be in RAM; encode lower 11 bits
                AddressingMode.Indirect =>
                    EncodeIndirect(resolvedValue, normalizedValue, line),

                // MMIO/RAM: no extra validation here; take lower 11 bits
                AddressingMode.MMIO =>
                    (ushort)((0b101 << 11) | (normalizedValue & MemoryMap.ImmediateMaxValue)),

                AddressingMode.RAM =>
                    (ushort)((0b100 << 11) | (normalizedValue & MemoryMap.ImmediateMaxValue)),

                // ROM: must fit in 13 bits
                AddressingMode.ROM =>
                    EncodeRom(normalizedValue, instr.IsJump, line),

                _ => throw new InvalidOperationException($"Unsupported addressing mode: {mode} (line {line.Source.Line})")
            };

            yield return (ushort)((instr.Instruction.ToOpcode() << 14) | addressBits);
        }

        private List<ushort> EncodeDataDirective(
            ParsedDataDirectives dataDirective,
            uint programCounter,
            IReadOnlyDictionary<string, uint> labelAddresses,
            bool resolveValue = true)
        {
            var words = new List<ushort>();
            var elements = AssemblyStringUtils.SplitDataElements(dataDirective.ValuePart);

            foreach (var element in elements)
            {
                var el = element.Trim();

                if (el.StartsWith('"') && el.EndsWith('"') && el.Length >= 2)
                {
                    // String literal
                    var str = AssemblyStringUtils.UnescapeString(el[1..^1]);
                    foreach (var ch in str)
                        words.Add((ushort)ch);
                }
                else if (el.StartsWith('\'') && el.EndsWith('\'') && el.Length >= 3)
                {
                    // Character literal
                    var character = AssemblyStringUtils.UnescapeString(el[1..^1]);
                    if (character.Length != 1)
                        throw new InvalidOperationException($"Invalid character literal: {el}");
                    words.Add((ushort)character[0]);
                }
                else
                {
                    // Numeric or expression
                    uint value = resolveValue ? ResolveValue(el, programCounter, labelAddresses) : 0;
                    switch (dataDirective.Directive)
                    {
                        case AssemblerConstants.D16Directive:
                            words.Add((ushort)(value & ushort.MaxValue));
                            break;
                        case AssemblerConstants.D32Directive:
                            words.Add((ushort)(value & ushort.MaxValue));
                            words.Add((ushort)((value >> 16) & ushort.MaxValue));
                            break;
                        case AssemblerConstants.DataDirective:
                            words.Add((ushort)(value & ushort.MaxValue));
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown data directive: {dataDirective.Directive}");
                    }
                }
            }

            return words;
        }

        private uint ResolveValue(
            string operand,
            uint programCounter,
            IReadOnlyDictionary<string, uint> labelAddresses)
        {
            string expression = operand.TrimStart(
                AssemblerConstants.ImmediatePrefix,
                AssemblerConstants.IndirectPrefix);

            // Direct numeric literal (no substitution needed)
            if (AssemblyStringUtils.TryParseDirectNumeric(expression, out uint directValue))
                return directValue;

            // Simple label or constant reference (no operators)
            if (TryResolveSingleIdentifier(expression, labelAddresses, out uint singleValue))
                return singleValue;

            // Otherwise, complex expression requiring full expression evaluation
            return ResolveComplexExpression(expression, programCounter, labelAddresses);
        }

        private bool TryResolveSingleIdentifier(
            string expression,
            IReadOnlyDictionary<string, uint> labelAddresses,
            out uint result)
        {
            result = 0;

            // Quick check: expression contains operators
            if (expression.IndexOfAny(_expressionOperators) >= 0)
                return false;

            // Label lookup
            if (labelAddresses.TryGetValue(expression, out var labelValue))
            {
                result = labelValue;
                return true;
            }
            // Constant lookup
            if (parser.GetConstants().TryGetValue(expression, out var constValue))
            {
                result = (uint)constValue;
                return true;
            }

            return false;
        }

        private uint ResolveComplexExpression(
            string expression,
            uint programCounter,
            IReadOnlyDictionary<string, uint> labelAddresses)
        {
            // Substitute $ with program counter
            expression = _programCounterRegex.Replace(expression, programCounter.ToString());

            // Substitute labels (longest first to avoid partial matches)
            foreach (var kvp in labelAddresses.OrderByDescending(k => k.Key.Length))
            {
                expression = Regex.Replace(
                    expression,
                    $@"(?<![A-Za-z0-9_]){Regex.Escape(kvp.Key)}(?![A-Za-z0-9_])",
                    kvp.Value.ToString());
            }

            // Substitute constants (longest first)
            foreach (var kvp in parser.GetConstants().OrderByDescending(k => k.Key.Length))
            {
                expression = Regex.Replace(
                    expression,
                    $@"(?<![A-Za-z0-9_]){Regex.Escape(kvp.Key)}(?![A-Za-z0-9_])",
                    kvp.Value.ToString());
            }

            // Convert numeric literals to decimal
            expression = AssemblyStringUtils.ReplaceNumericLiterals(expression);

            // Evaluate arithmetic expression
            try
            {
                var expr = new Expression(expression);
                var result = expr.Evaluate();
                return Convert.ToUInt32(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve operand expression: {Operand}", expression);
                throw new InvalidOperationException($"Invalid operand expression: {expression}");
            }
        }

        private static uint ComputeBootloaderEndAddress(
            List<Line> lines,
            Dictionary<Line, ushort> lineToRomAddress)
        {
            var lastBootLine = lines
                .Where(l => l.Bank == AssemblerConstants.ProgramBankName &&
                            l.Source.Source.Identifier == AssemblerConstants.BootloaderResourceName)
                .LastOrDefault();

            if (lastBootLine == null || !lineToRomAddress.TryGetValue(lastBootLine, out var addr))
                return 0;

            return (uint)(addr + 1);
        }

        private static AddressingMode GetAddressingMode(string operand, uint address)
        {
            operand = operand.Trim();

            // Explicit immediate mode (#value)
            if (operand.StartsWith(AssemblerConstants.ImmediatePrefix))
                return AddressingMode.Immediate;

            // Explicit indirect mode (@RAM_address)
            if (operand.StartsWith(AssemblerConstants.IndirectPrefix))
            {
                if (!MemoryMap.Ram.Contains(address))
                    throw new InvalidOperationException(
                        $"Indirect addressing requires RAM address (got 0x{address:X4})");
                return AddressingMode.Indirect;
            }

            // Inferred modes based on address range
            if (MemoryMap.Mmio.Contains(address))
                return AddressingMode.MMIO;

            if (MemoryMap.Ram.Contains(address))
                return AddressingMode.RAM;

            if (MemoryMap.Rom.Contains(address))
                return AddressingMode.ROM;

            throw new InvalidOperationException(
                $"Address 0x{address:X4} does not map to any known memory region");
        }

        // Builds the sorted threshold list used by FindFixedPointN.
        // For each operand: Ti = max N (injected constants) it can tolerate before needing wrapping.
        // Operands that already overflow at N=0 go directly to alwaysConstantIndices; unconstrained ones (RAM/MMIO) are omitted.
        private static (List<Threshold> finiteThresholds, List<int> alwaysConstantIndices)
            CalculateThresholds(List<OperandNeedsConstantAnalysis> operandAnalyses)
        {
            var finiteThresholds = new List<Threshold>();
            var alwaysConstantIndices = new List<int>();

            for (int i = 0; i < operandAnalyses.Count; i++)
            {
                var (ti, alwaysConstant) = CalculateTi(operandAnalyses[i]);

                if (alwaysConstant)
                    alwaysConstantIndices.Add(i);
                else if (ti < int.MaxValue)
                    finiteThresholds.Add(new Threshold(ti, i));
            }

            finiteThresholds.Sort((a, b) => a.Ti.CompareTo(b.Ti));
            return (finiteThresholds, alwaysConstantIndices);
        }

        // Returns Ti = 0x7FF - normalizedValue: the number of constants this operand can absorb before its
        // address exceeds the 11-bit limit. Returns AlwaysConstant=true if it already overflows at N=0.
        private static (int Ti, bool AlwaysConstant) CalculateTi(OperandNeedsConstantAnalysis analysis)
        {
            // Only Immediate mode and ROM jumps can benefit from constant injection
            // All other modes are either unconstrained or will fail at encoding if invalid

            if (analysis.Mode == AddressingMode.Immediate ||
                (analysis.Mode == AddressingMode.ROM && analysis.IsJump))
            {
                // 11-bit max for immediate values and jump targets
                int maxValue = (int)MemoryMap.ImmediateMaxValue; // 0x7FF
                int currentValue = (int)analysis.NormalizedValue;
                int threshold = maxValue - currentValue;

                if (threshold < 0)
                {
                    // Value exceeds 11-bit limit even at N=0 → must be wrapped in constant
                    return (0, AlwaysConstant: true);
                }

                // Can accommodate 'threshold' constants before exceeding limit
                return (threshold, AlwaysConstant: false);
            }

            // All other modes are unconstrained:
            // - Indirect: pointer in RAM, no constraint on pointed address
            // - RAM: 11-bit offset always valid within RAM range
            // - MMIO: 11-bit offset always valid within MMIO range
            // - ROM direct (non-jump): 13-bit encoding; if > 0x1FFF, it's an encoding error
            //   (not fixable by constant injection, will be caught in EncodeInstruction)
            return (int.MaxValue, AlwaysConstant: false);
        }

        // Solves N = alwaysConstantCount + count(Ti < N) analytically by walking the sorted threshold list.
        // Avoids iterative relaxation: converges in O(k) after the O(k log k) sort in CalculateThresholds.
        private static int FindFixedPointN(List<Threshold> finiteThresholds, int alwaysConstantCount)
        {
            int n = alwaysConstantCount;
            int thresholdIndex = 0;

            while (true)
            {
                // Advance through thresholds while Ti < n
                while (thresholdIndex < finiteThresholds.Count && finiteThresholds[thresholdIndex].Ti < n)
                {
                    thresholdIndex++;
                }

                int computedN = alwaysConstantCount + thresholdIndex;

                if (computedN == n)
                {
                    // Fixed point found
                    return n;
                }

                n = computedN;

                // Safety check: N should not exceed total operand count
                if (n > alwaysConstantCount + finiteThresholds.Count)
                {
                    throw new InvalidOperationException("Fixed-point computation failed to converge");
                }
            }
        }

        private static ushort EncodeImmediate(uint normalizedValue, Line line)
        {
            if (normalizedValue > MemoryMap.ImmediateMaxValue)
                throw new InvalidOperationException(
                    $"Immediate value 0x{normalizedValue:X4} exceeds 11-bit limit (0x{MemoryMap.ImmediateMaxValue:X4}). " +
                    $"Line {line.Source.Line}: {line.Content}");
            return (ushort)((0b110 << 11) | (normalizedValue & MemoryMap.ImmediateMaxValue));
        }

        private static ushort EncodeIndirect(uint resolvedValue, uint normalizedValue, Line line)
        {
            if (!MemoryMap.Ram.Contains(resolvedValue))
                throw new InvalidOperationException(
                    $"Indirect addressing requires RAM address (got 0x{resolvedValue:X4}). " +
                    $"Line {line.Source.Line}: {line.Content}");
            return (ushort)((0b111 << 11) | (normalizedValue & MemoryMap.ImmediateMaxValue));
        }

        private static ushort EncodeRom(uint normalizedValue, bool isJump, Line line)
        {
            if (normalizedValue > MemoryMap.MaxDirectRomAddress)
                throw new InvalidOperationException(
                    $"ROM address 0x{normalizedValue:X4} exceeds 13-bit limit (0x{MemoryMap.MaxDirectRomAddress:X4}). " +
                    $"Line {line.Source.Line}: {line.Content} " +
                    (isJump ? "(jump constant address too high)" : "(direct ROM address too high)"));
            // Mode bits 0b000 => encoded value is the 13-bit address
            return (ushort)(normalizedValue & MemoryMap.MaxDirectRomAddress);
        }

        /// <summary>
        /// Records operand analysis data for fixed-point computation.
        /// </summary>
        private sealed record OperandNeedsConstantAnalysis(
            uint NormalizedValue,
            AddressingMode Mode,
            bool IsJump,
            string Operand,
            ushort ProgramCounter);

        /// <summary>
        /// Records threshold data for fixed-point analysis.
        /// </summary>
        private sealed record Threshold(int Ti, int OperandIndex);
    }
}