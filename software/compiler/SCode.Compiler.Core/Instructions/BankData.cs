namespace SCode.Compiler.Instructions
{
    internal class BankData
    {
        public bool HasLabel => !string.IsNullOrEmpty(Label);
        public string? Label { get; set; }
        public string Value { get; set; }
        public object RawValue { get; set; }

        public override string ToString() => HasLabel ? $"{Label}: {Value}" : $"{Value}";
    }
}
