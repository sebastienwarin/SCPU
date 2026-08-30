namespace SCPU.Architecture
{
    /// <summary>
    /// 2-bit S-CPU instructions.
    /// </summary>
    public enum Instruction : byte
    {
        /// <summary>
        /// Logical NOR operation: A = ~(A | operand).
        /// </summary>
        NOR = 0b00,
        
        /// <summary>
        /// Arithmetic ADD operation: A = A + operand.
        /// </summary>
        ADD = 0b01,

        /// <summary>
        /// Store Accumulator: writes the current value of A into memory at the given address.
        /// </summary>
        STA = 0b10,
        
        /// <summary>
        /// Jump if Carry Clear: updates the program counter to the target address
        /// only if the Carry flag is clear (C = 0).
        /// </summary>
        JCC = 0b11
    }
}
