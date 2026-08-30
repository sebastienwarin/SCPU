namespace SCPU.Assembler.Model
{
    /// <summary>
    /// Represents the location of a line in an assembly source file or inline source.
    /// Contains metadata useful for diagnostics, such as the source identifier,
    /// line number, and the original unprocessed line text.
    /// </summary>
    /// <param name="Source">The underlying <see cref="SourceDocument"/> (file or inline).</param>
    /// <param name="Line">The line number in the source.</param>
    /// <param name="RawContent">The raw text of the line, before any preprocessing.</param>
    public record SourceRange(SourceDocument Source, int Line, string RawContent)
    {
        /// <summary>
        /// Returns a human-readable representation for diagnostics/logging.
        /// </summary>
        public override string ToString() => $"{Source.Identifier}:{Line}";
    }
}
