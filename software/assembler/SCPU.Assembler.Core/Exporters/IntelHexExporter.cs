using System.Text;

namespace SCPU.Assembler.Exporters
{
    /// <summary>
    /// Intel Hex exporter.
    /// </summary>
    public sealed class IntelHexExporter : IAssemblyExporter
    {
        public OutputFormat Format => OutputFormat.IntelHex;

        public byte[] Convert(AssemblyResult result) => Encoding.UTF8.GetBytes(GenerateIntelHex(result.Binary));

        private static string GenerateIntelHex(byte[] data)
        {
            var sb = new StringBuilder();
            int address = 0;
            // Process data in 16-byte lines (standard for Intel HEX)
            for (int i = 0; i < data.Length; i += 16)
            {
                int len = Math.Min(16, data.Length - i);
                sb.Append($":{len:X2}{address:X4}00");
                byte checksum = (byte)(len + (address >> 8) + (address & 0xFF));
                for (int j = 0; j < len; j++)
                {
                    sb.Append($"{data[i + j]:X2}");
                    checksum += data[i + j];
                }
                checksum = (byte)(-checksum);
                sb.AppendLine($"{checksum:X2}");
                address += len;
            }
            // Intel HEX EOF line
            sb.AppendLine(":00000001FF");
            return sb.ToString();
        }
    }
}
