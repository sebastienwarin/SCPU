using System.Text;

namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Logisim-16 hex exporter.
    /// </summary>
    public sealed class Logisim16Exporter : IAssemblyExporter
    {
        public OutputFormat Format => OutputFormat.Logisim16;

        public byte[] Convert(AssemblyResult result) => Encoding.UTF8.GetBytes(GenerateLogisim16(result.Binary));

        private static string GenerateLogisim16(byte[] data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("v2.0 raw"); // Logisim16 header

            int wordsOnLine = 0;
            for (int i = 0; i < data.Length; i += 2)
            {
                // Each 16-bit word is formed by two consecutive bytes (big-endian)
                ushort word = i + 1 < data.Length
                    ? (ushort)((data[i] << 8) | data[i + 1])
                    : (ushort)(data[i] << 8);

                sb.AppendFormat("{0:x4}", word);
                wordsOnLine++;

                // Insert line break after 8 words per line for readability (Logisim default)
                if (wordsOnLine == 8)
                {
                    sb.AppendLine();
                    wordsOnLine = 0;
                }
                else
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }
    }
}
