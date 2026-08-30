using SCPU.Architecture;

namespace SCPU.Assembler.Model
{
    /// <summary>
    /// Represents the parsed components of an instruction line.
    /// Cached during construction to avoid repeated parsing.
    /// </summary>
    public readonly struct ParsedInstruction
    {
        /// <summary>
        /// The instruction (e.g., "ADD", "JCC", "STA").
        /// </summary>
        public required Instruction Instruction { get; init; }

        /// <summary>
        /// The operand string (e.g., "#42", "LABEL", "@0x12100").
        /// </summary>
        public required string Operand { get; init; }

        /// <summary>
        /// True if this is a jump instruction (e.g., JCC).
        /// </summary>
        public bool IsJump => Instruction == Instruction.JCC;
    }
}
