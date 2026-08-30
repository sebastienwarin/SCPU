namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Defines an exporter that can convert an <see cref="AssemblyResult"/> into a specific format.
    /// </summary>
    public interface IAssemblyExporter
    {
        /// <summary>
        /// The format this exporter supports.
        /// </summary>
        OutputFormat Format { get; }

        /// <summary>
        /// Converts an <see cref="AssemblyResult"/> into the exporter's <see cref="OutputFormat"/>.
        /// </summary>
        /// <param name="result">The assembly result to export.</param>
        /// <returns>A byte array containing the formatted output.</returns>
        byte[] Convert(AssemblyResult result);
    }
}
