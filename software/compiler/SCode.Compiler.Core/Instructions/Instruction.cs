namespace SCode.Compiler.Instructions
{
    public class Instruction(string mnemonic, params string[] operands)
    {
        public string Mnemonic { get; set; } = mnemonic.ToUpper();
        public List<string> Operands { get; set; } = new List<string>(operands);

        public override string ToString()
        {
            return Operands.Count > 0 ? $"{Mnemonic} {string.Join(", ", Operands)}" : Mnemonic;
        }

        public static Instruction Create(string mnemonic)
        {
            return new Instruction(mnemonic);
        }

        public static Instruction Create(string mnemonic, params ValueOrAddress[] operands)
        {
            return new Instruction(mnemonic, operands.Select(o => o.ToString()).ToArray());
        }

        public static Instruction Create(string mnemonic, params string[] operands)
        {   
            return new Instruction(mnemonic, operands);
        }
    }
}
