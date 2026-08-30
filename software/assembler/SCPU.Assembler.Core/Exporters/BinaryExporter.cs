namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Raw binary exporter.
    /// </summary>
    public sealed class BinaryExporter : IAssemblyExporter
    {
        public OutputFormat Format => OutputFormat.Binary;

        public byte[] Convert(AssemblyResult result) => result.Binary;
    }
}
