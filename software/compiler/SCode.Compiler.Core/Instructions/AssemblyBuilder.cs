using SCPU.Assembler;
using System.Text;

namespace SCode.Compiler.Instructions
{
    internal class AssemblyBuilder
    {
        private const int WORD_SIZE = 16;

        public List<string> Includes { get; } = [];
        public Dictionary<string, object> Constants { get; } = [];
        public Dictionary<BankType, List<BankData>> Banks { get; } = [];

        public AssemblyBuilder()
        {
            AddBank(BankType.Program);
            AddBank(BankType.ProgramData);
            AddBank(BankType.UserPage);
        }

        public void AddBank(BankType bankType)
        {
            Banks.Add(bankType, []);
        }

        public void AddBankData(BankType bankType, BankData data)
        {
            if (!Banks.ContainsKey(bankType))
            {
                AddBank(bankType);
            }
            Banks[bankType].Add(data);
        }

        public void AddData(BankType bankType, string identifier, object value, bool ignoreValueEncoding = false)
        {
            ushort size = WORD_SIZE;
            string? strData = ignoreValueEncoding && value != null ? value.ToString() : GetStringData(value);
            if (strData == null)
            {
                throw new ArgumentException($"Unable to emit the data assembly for the variable '{identifier}' : invalid data type");
            }
            else if (value is long || value is long[])
            {
                size *= 2;
            }
            this.AddBankData(bankType, new BankData
            {
                Label = identifier,
                Value = $"{AssemblerConstants.DataDirective}{size} {strData}",
                RawValue = value
            });
        }

        public void AddMemoryReservation(string identifier, object size)
        {
            this.AddBankData(BankType.UserPage, new BankData
            {
                Label = identifier,
                Value = $"{AssemblerConstants.ResDirective} {size}"
            });
        }

        public bool TryAddUniqueData(BankType bankType, string identifier, object value, out string finalIdentifier)
        {
            if (Banks.ContainsKey(bankType))
            {
                // Reuse existing data is value already exists
                var existingData = Banks[bankType].FirstOrDefault(o => o.RawValue.Equals(value));
                if (existingData != null)
                { 
                    finalIdentifier = existingData.Label;
                    return false;
                }

                // Randomize identifier if already exists
                var existingIdentifier = Banks[bankType].FirstOrDefault(o => o.Label.Equals(identifier));
                if (existingIdentifier != null)
                {
                    identifier = $"{existingIdentifier.Label}_{RandomGenerator.RandomString()}";
                }
            }

            // Add data
            AddData(bankType, identifier, value);
            finalIdentifier = identifier;
            return true;
        }

        public void OptimizeProgram()
        {
            bool isOptimized;
            do
            {
                var program = Banks[BankType.Program];
                if ((isOptimized = AssemblyOptimizer.ProcessLines(ref program)))
                {
                    Banks[BankType.Program] = program;
                }
            }
            while (isOptimized);
        }

        public string GenerateAssembly(bool optimize = true)
        {
            var sb = new StringBuilder();

            // Optimize assembly code
            if (optimize)
            {
                OptimizeProgram();
            }

            // Constants
            foreach (var constant in Constants)
            {
                sb.AppendLine($"{AssemblerConstants.ConstDirective} {constant.Key} = {constant.Value}");
            }

            // Include files
            foreach (var includeFile in Includes)
            {
                sb.AppendLine($"{AssemblerConstants.IncludeDirective} \"{includeFile}\"");
            }

            // User page reservation
            sb.AppendLine($"{AssemblerConstants.BankDirective} {AssemblerConstants.UserPageBankName}");
            AppendBankDatas(sb, BankType.UserPage);

            // Program data
            sb.AppendLine($"{AssemblerConstants.BankDirective} {AssemblerConstants.ProgramBankName}");
            AppendBankDatas(sb, BankType.ProgramData);

            // Program after entry point
            sb.AppendLine($"{AssemblerConstants.EntryPointLabel}:");
            AppendBankDatas(sb, BankType.Program);

            // Return ASM code
            return sb.ToString();
        }

        private void AppendBankDatas(StringBuilder sb, BankType bankType)
        {
            foreach (var data in Banks[bankType])
            {
                if (data.HasLabel)
                {
                    sb.Append($"{data.Label}: ");
                }
                sb.AppendLine(data.Value);
            }
        }

        // TODO: move logic to Literal class
        private static string? GetStringData(object? data)
        {
            // Helper: format an unsigned 16-bit value as decimal string
            static string U16(ulong v) => ((ushort)(v & 0xFFFF)).ToString();

            // Helper: split a 32-bit unsigned into "MSB,LSB"
            static string SplitU32(uint v) => $"{U16((v >> 16) & 0xFFFFu)}, {U16(v & 0xFFFFu)}";

            if (data is object[] datas)
            {
                return string.Join(",", datas.Select(d => GetStringData(d)));
            }
            else if (data is string str)
            {
                // Emit each char as a quoted escaped char, then a terminating 0
                return string.Join(",", str.Select(c => $"\"{c.Escape()}\"")) + (str.Length > 0 ? "," : "") + "0";
            }
            else if (data is char ch)
            {
                return $"\"{ch.Escape()}\"";
            }
            else if (data is bool boolean)
            {
                return boolean ? "1" : "0";
            }
            // --- 16-bit scalars → emit as unsigned 16-bit decimal ---
            else if (data is ushort u16)
            {
                return U16(u16);
            }
            else if (data is short s16)
            {
                return U16(unchecked((ushort)s16)); // force unsigned form (two's complement)
            }
            // --- 32-bit scalars → split into two 16-bit words: MSB,LSB ---
            else if (data is uint u32)
            {
                return SplitU32(u32);
            }
            else if (data is int s32)
            {
                return SplitU32(unchecked((uint)s32)); // preserve bit pattern, emit as two u16
            }
            // --- Fixed-point Q10 on decimal → clamp/convert to unsigned 16-bit decimal ---
            else if (data is decimal dec)
            {
                // TODO: WIP
                if (dec < -32.0m || dec >= 32.0m)
                    throw new OverflowException("Out of Q10 bounds");

                short q10 = (short)Math.Round(dec * 1024m);
                return U16(unchecked((ushort)q10)); // emit as unsigned decimal (no negatives)
            }
            else
            {
                return null;
            }
        }
    }
}
