namespace SCode.Compiler
{
    public static class Registers
    {
        public const string R0 = nameof(R0);
        public const string R1 = nameof(R1);
        public const string R2 = nameof(R2);
        public const string R3 = nameof(R3);
        public const string R4 = nameof(R4);
        public const string R5 = nameof(R5);
        public const string R6 = nameof(R6);
        public const string R7 = nameof(R7);
        public const string R8 = nameof(R8);
        public const string R9 = nameof(R9);

        public const string RParameter = "RPAR";
        public const string RReturnAddress = "RRET";
        public const string RPeek = "RPEEK";

        public const string FramePointer = "FP";
        public const string StackPointer = "SP";

        public const string TemporaryVariables = "TEMPVAR";
        public const string CarryFlag = "CF";
    }
}
