namespace SCode.Compiler.Ast.Statements
{
    public class DoWhileStatement : WhileStatement, BreakStatement.IBreakableFlow, ContinueStatement.IContinuableFlow
    {
        public new string ContinueLabel { get; } = RandomGenerator.RandomStringLabel("dowhile_continue");

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
            var enterLabel = RandomGenerator.RandomStringLabel("dowhile_enter");

            // Set enter label
            builder.SetLabel(enterLabel);

            // Execute body
            Body?.Build();

            // Build condition and emit branch
            builder.SetLabel(ContinueLabel);
            Condition.Build();
            builder.EmitJumpIfNotZero(enterLabel);

            // Exit
            builder.SetLabel(BreakLabel);
        }

        public override string ToString() => $"DoWhile {Condition}";
    }
}
