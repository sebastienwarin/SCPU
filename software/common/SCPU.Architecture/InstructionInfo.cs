namespace SCPU.Architecture
{
    /// <summary>
    /// Centralized helpers for S-CPU Instruction: opcode/mnemonic parsing & formatting.
    /// </summary>
    public static class InstructionInfo
    {
        private static readonly string[] Mnemonics =
        {
            "nor",
            "add",
            "sta",
            "jcc"
        };

        /// <summary>
        /// Gets the lowercase mnemonic for an instruction (e.g. Instruction.ADD -> "add").
        /// </summary>
        public static string ToMnemonic(this Instruction instruction) => Mnemonics[(int)instruction];

        /// <summary>
        /// Tries to parse a mnemonic (case-insensitive) to an <see cref="Instruction"/>.
        /// Returns false on unknown mnemonic.
        /// </summary>
        public static bool TryParseMnemonic(string? text, out Instruction instruction)
        {
            instruction = default;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Very small set -> switch on normalized string (no allocations beyond ToLowerInvariant)
            switch (text.Trim().ToLowerInvariant())
            {
                case "nor": instruction = Instruction.NOR; return true;
                case "add": instruction = Instruction.ADD; return true;
                case "sta": instruction = Instruction.STA; return true;
                case "jcc": instruction = Instruction.JCC; return true;
                default: return false;
            }
        }

        /// <summary>
        /// Gets the 2-bit opcode value for an instruction (0..3).
        /// </summary>
        public static ushort ToOpcode(this Instruction instruction) => (ushort)instruction;

        /// <summary>
        /// Tries to convert a raw opcode (0..3) to an <see cref="Instruction"/>.
        /// Returns false if the value is outside the 2-bit range.
        /// </summary>
        public static bool TryFromOpcode(ushort opcode, out Instruction instruction)
        {
            if (opcode < Mnemonics.Length)
            {
                instruction = (Instruction)opcode;
                return true;
            }
            instruction = default;
            return false;
        }

        /// <summary>
        /// Returns true if the string is a valid mnemonic (case-insensitive).
        /// Fast path for lexers/parsers.
        /// </summary>
        public static bool IsMnemonic(string? text) => TryParseMnemonic(text, out _);
    }
}
