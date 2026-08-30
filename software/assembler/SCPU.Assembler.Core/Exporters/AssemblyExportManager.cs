using Microsoft.Extensions.Logging;

namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Central manager responsible for converting assembly results into various output formats.
    /// Uses dependency-injected <see cref="IAssemblyExporter"/> implementations to perform conversions.
    /// </summary>
    public sealed class AssemblyExportManager(IEnumerable<IAssemblyExporter> exporters, ILogger<AssemblyExportManager> logger)
    {
        private readonly IReadOnlyDictionary<OutputFormat, IAssemblyExporter> _exporters = exporters.ToDictionary(e => e.Format);

        /// <summary>
        /// Converts the given <paramref name="result"/> into the specified <paramref name="format"/>.
        /// </summary>
        /// <param name="result">The assembly result to export.</param>
        /// <param name="format">The desired output format.</param>
        /// <returns>A byte array representing the converted result (contents depend on the format).</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if no exporter is registered for the specified format.</exception>
        public byte[] Convert(AssemblyResult result, OutputFormat format)
        {
            if (!_exporters.TryGetValue(format, out var exporter))
                throw new ArgumentOutOfRangeException(nameof(format), format, "No exporter registered for this format.");

            return exporter.Convert(result);
        }

        /// <summary>
        /// Writes an <see cref="AssemblyResult"/> to the specified <paramref name="outputFile"/> 
        /// in the given <paramref name="format"/>.
        /// </summary>
        /// <param name="result">The assembly result to export.</param>
        /// <param name="outputFile">The target file where the output will be written.</param>
        /// <param name="format">The output format to use when converting the assembly result.</param>
        /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        public async Task WriteAsync(AssemblyResult result, FileInfo outputFile, OutputFormat format, CancellationToken ct = default)
        {
            var payload = Convert(result, format);
            await WriteAsync(payload, outputFile, format, ct);
        }

        /// <summary>
        /// Writes a raw payload to the specified <paramref name="outputFile"/> 
        /// using the given <paramref name="format"/>.
        /// </summary>
        /// <param name="payload">The raw byte array to write.</param>
        /// <param name="outputFile">The target file where the payload will be written.</param>
        /// <param name="format">The output format associated with the payload.</param>
        /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        public async Task WriteAsync(byte[] payload, FileInfo outputFile, OutputFormat format, CancellationToken ct = default)
        {
            logger.LogInformation("Writing {Format} file: {File}", format, outputFile.FullName);
            Directory.CreateDirectory(outputFile.Directory!.FullName);
            await File.WriteAllBytesAsync(outputFile.FullName, payload, ct);
        }
    }
}
