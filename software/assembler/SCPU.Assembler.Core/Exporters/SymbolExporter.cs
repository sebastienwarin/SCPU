using System.Text;

namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Exports all resolved symbols (constants and labels) as a plain text list,
    /// one symbol per line, in the form: <c>NAME=0xXXXX</c>.
    /// </summary>
    public sealed class SymbolExporter : IAssemblyExporter
    {
        public OutputFormat Format => OutputFormat.Symbol;

        /// <inheritdoc />
        public byte[] Convert(AssemblyResult result)
        {
            var sb = new StringBuilder();

            // Constants
            foreach (var (constant, value) in result.Constants)
            {
                sb.AppendLine($"{constant}=0x{value:x4}");
            }

            // Labels
            foreach (var (label, address) in result.Labels)
            {
                sb.AppendLine($"{label}=0x{address:x4}");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}