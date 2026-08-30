namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Supported output formats for exporting assembled binaries.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// Annotated format (human-readable, useful for debugging).
        /// </summary>
        Annotated,

        /// <summary>
        /// Raw binary format.
        /// </summary>
        Binary,

        /// <summary>
        /// Intel HEX format.
        /// </summary>
        IntelHex,

        /// <summary>
        /// Logisim-16 hex format.
        /// </summary>
        Logisim16,

        /// <summary>
        /// Verilog memory initialization file.
        /// </summary>
        Verilog,

        /// <summary>
        /// Gowin FPGA memory initialization file.
        /// </summary>
        Gowin,

        /// <summary>
        /// Plain-text symbol table (label-to-address) output.
        /// </summary>
        Symbol
    }
}
