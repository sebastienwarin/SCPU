using SCPU.Architecture;

namespace SCPU.Assembler.Model
{
    /// <summary>
    /// Represents a fully parsed line of assembly, including its content, labels, bank, and cached structural information.
    /// </summary>
    /// <remarks>
    /// This record caches the line type and parsed components (mnemonic/operand, directive, etc.)
    /// to avoid repeated Split(), StartsWith(), and string operations during constant patching.
    /// </remarks>
    /// <param name="Source">The source location metadata (<see cref="SourceRange"/>).</param>
    /// <param name="Content">The normalized line content (after preprocessing, macro expansion, etc.).</param>
    /// <param name="Labels">A list of labels associated with this line (can be empty).</param>
    /// <param name="Bank">The memory bank this line belongs to (e.g., "prg", "userpage").</param>
    /// <param name="Type">The cached line type (Instruction, DataDirective, ResDirective, or Other).</param>
    /// <param name="Instruction">Cached mnemonic, operand, and isJump flag (populated if LineType is Instruction).</param>
    /// <param name="DataDirective">Cached directive and value part (populated if LineType is DataDirective).</param>
    /// <param name="ReservationDirective">Cached size expression (populated if LineType is ReservationDirective).</param>
    public record Line(
        SourceRange Source,
        string Content,
        List<string> Labels,
        string? Bank,
        LineType Type = LineType.Unknown,
        ParsedInstruction? Instruction = null,
        ParsedDataDirectives? DataDirective = null,
        ParsedReservationDirective? ReservationDirective = null)
    {
        /// <summary>
        /// Factory method to create a <see cref="Line"/> with automatic parsing of structural information.
        /// This method identifies the line type and pre-parses operands/directives to avoid repeated parsing.
        /// </summary>
        /// <param name="source">The source location metadata.</param>
        /// <param name="content">The normalized line content.</param>
        /// <param name="labels">Labels associated with this line.</param>
        /// <param name="bank">The memory bank.</param>
        /// <returns>A fully populated <see cref="Line"/> with cached parsing information.</returns>
        public static Line ParseAndCreate(SourceRange source, string content, List<string> labels, string? bank)
        {
            var lineType = LineType.Unknown;
            ParsedInstruction? instrParts = null;
            ParsedDataDirectives? dataParts = null;
            ParsedReservationDirective? resParts = null;

            var parts = content.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return new Line(source, content, labels, bank, lineType);
            }

            string firstToken = parts[0].Trim();

            // Check for data directive
            if (firstToken == AssemblerConstants.DataDirective ||
                firstToken == AssemblerConstants.D16Directive ||
                firstToken == AssemblerConstants.D32Directive)
            {
                lineType = LineType.DataDirective;
                dataParts = new ParsedDataDirectives
                {
                    Directive = firstToken,
                    ValuePart = parts.Length > 1 ? parts[1].Trim() : "0"
                };
            }
            // Check for reservation directive
            else if (firstToken == AssemblerConstants.ResDirective)
            {
                lineType = LineType.ReservationDirective;
                resParts = new ParsedReservationDirective
                {
                    SizeExpression = parts.Length > 1 ? parts[1].Trim() : "0"
                };
            }
            // Check for instruction (mnemonic)
            else if (InstructionInfo.TryParseMnemonic(firstToken, out var instruction))
            {
                lineType = LineType.Instruction;
                instrParts = new ParsedInstruction
                {
                    Instruction = instruction,
                    Operand = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                };
            }
            else
            {
                lineType = LineType.Other;
            }

            return new Line(source, content, labels, bank, lineType, instrParts, dataParts, resParts);
        }

        /// <summary>
        /// Returns a concise human-readable representation for diagnostics/logging.
        /// Format: <c>{Source.Identifier}:{Source.Line} [{Bank}] {Content} (Labels: ...)</c>
        /// </summary>
        public override string ToString()
        {
            var labels = Labels.Count > 0 ? $" Labels: {string.Join(",", Labels)}" : "";
            var bank = !string.IsNullOrEmpty(Bank) ? $" [{Bank}]" : "";
            return $"{Source}{bank}{labels}";
        }
    }
}
