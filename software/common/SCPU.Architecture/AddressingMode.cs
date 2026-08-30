namespace SCPU.Architecture
{
    /// <summary>
    /// Supported addressing modes for S-CPU instruction operands.
    /// Encoded in the upper 3 bits of the 14-bit operand field.
    /// </summary>
    public enum AddressingMode
    {
        /// <summary>
        /// '0AA' - ROM addressing.
        /// 13-bit operand (0x0000-0x1FFF, 8K words).
        /// </summary>
        ROM = 0,

        /// <summary>
        /// '100' - RAM addressing.
        /// 11-bit operand (0x000-0x7FF, 2K words, virtual range 0x12000-0x127FF).
        /// </summary>
        RAM = 4,

        /// <summary>
        /// '101' - MMIO addressing.
        /// 11-bit operand (0x000-0x7FF, 2K words, virtual range 0x12800-0x12FFF).
        /// </summary>
        MMIO = 5,

        /// <summary>
        /// '110' - Immediate value.
        /// 11-bit literal encoded directly in the instruction.
        /// </summary>
        Immediate = 6,

        /// <summary>
        /// '111' - Indirect (pointer) addressing.
        /// 11-bit operand interpreted as a RAM address (e.g., <c>@var</c>).
        /// </summary>
        Indirect = 7
    }
}
