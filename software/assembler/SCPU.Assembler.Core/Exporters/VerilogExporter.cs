using System.Text;

namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Icarus Verilog memory initialization file exporter.
    /// </summary>
    public sealed class VerilogExporter : IAssemblyExporter
    {
        public OutputFormat Format => OutputFormat.Verilog;        
        public byte[] Convert(AssemblyResult result)
        {
            var sb = new StringBuilder();
            foreach (var (_, word) in result.FinalWords)
            {
                sb.AppendLine($"{word:x4}");
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
