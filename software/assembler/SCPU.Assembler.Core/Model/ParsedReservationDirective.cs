namespace SCPU.Assembler.Model
{
    /// <summary>
    /// Represents the parsed components of a reservation directive line.
    /// Cached during construction to avoid repeated parsing.
    /// </summary>
    public readonly struct ParsedReservationDirective
    {
        /// <summary>
        /// The size expression (e.g., "256", "BUFFER_SIZE").
        /// </summary>
        public required string SizeExpression { get; init; }
    }
}
