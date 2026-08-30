namespace SCPU.Assembler
{
    public static class AssemblerConstants
    {
        // Resource names
        public const string BootloaderResourceName = "SCPU.Assembler.Resources.Bootloader.asm";
        public const string ConstantsResourceName = "SCPU.Assembler.Resources.Constants.asm";
        public const string MacrosResourcePrefix = "SCPU.Assembler.Resources.Macros.";
        public const string MacroResourceExtension = ".asm";

        // Bank names
        public const string ProgramBankName = "prg";
        public const string UserPageBankName = "userpage";

        // Directives
        public const string IfDirective = "#if";
        public const string ElifDirective = "#elif";
        public const string ElseDirective = "#else";
        public const string IncludeDirective = "#include";
        public const string BankDirective = "#bank";
        public const string ConstDirective = "#const";
        public const string ResDirective = "#res";
        public const string D16Directive = "#d16";
        public const string D32Directive = "#d32";
        public const string DataDirective = "#d";

        // Macro pattern
        public const string MacroPatternStart = "[macro ";

        // Labels
        public const string EntryPointLabel = "ENTRY_POINT";

        // Others
        public const char ImmediatePrefix = '#';
        public const char IndirectPrefix = '@';
        public const char ProgramCounterSymbol = '$';
        public const char CommentChar = ';';
    }
}
