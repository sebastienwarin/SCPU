using System.Text;

namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Gowin FPGA memory initialization file exporter.
    /// </summary>
    public sealed class GowinExporter : IAssemblyExporter
    {
        public OutputFormat Format => OutputFormat.Gowin;

        public byte[] Convert(AssemblyResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#File_format=Hex");
            sb.AppendLine($"#Address_depth=49152");
            sb.AppendLine("#Data_width=16");
            foreach (var (_, word) in result.FinalWords)
            {
                sb.AppendLine($"{word:x4}");
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
