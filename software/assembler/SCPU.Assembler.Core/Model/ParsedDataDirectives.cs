namespace SCPU.Assembler.Model
{
    /// <summary>
    /// Represents the parsed components of a data directive line.
    /// Cached during construction to avoid repeated parsing.
    /// </summary>
    public readonly struct ParsedDataDirectives
    {
        /// <summary>
        /// The directive type (e.g., "#d", "#d16", "#d32").
        /// </summary>
        public required string Directive { get; init; }

        /// <summary>
        /// The value portion (e.g., "0xFF", "42, 100", "\"hello\"").
        /// </summary>
        public required string ValuePart { get; init; }
    }
}
