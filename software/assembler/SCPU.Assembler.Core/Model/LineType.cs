namespace SCPU.Assembler.Model
{
    /// <summary>
    /// Represents the type of a parsed assembly line.
    /// </summary>
    public enum LineType
    {
        /// <summary>
        /// Unknown or comment line.
        /// </summary>
        Unknown,

        /// <summary>
        /// Instruction with mnemonic and operand (e.g., "ADD #42", "JCC LABEL").
        /// </summary>
        Instruction,

        /// <summary>
        /// Data directive (e.g., "#d 0xFF", "#d16 42, 100", "#d32 0x12345678").
        /// </summary>
        DataDirective,

        /// <summary>
        /// Reservation directive (e.g., "#res 256").
        /// </summary>
        ReservationDirective,

        /// <summary>
        /// Other directives or preprocessor-managed lines.
        /// </summary>
        Other
    }
}
