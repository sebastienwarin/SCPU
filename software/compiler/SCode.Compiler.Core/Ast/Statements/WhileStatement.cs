namespace SCode.Compiler.Ast.Statements
{
    public class WhileStatement : Statement, BreakStatement.IBreakableFlow, ContinueStatement.IContinuableFlow
    {
        public string ContinueLabel { get; } = RandomGenerator.RandomStringLabel("while_enter");
        public string BreakLabel { get; } = RandomGenerator.RandomStringLabel("while_exit");

        [ChildNode]
        public Expression Condition { get; set; }

        [ChildNode]
        public Block? Body { get; set; }

        [ChildNode]
        public Block? Else { get; set; }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
            var elseLabel = RandomGenerator.RandomStringLabel("while_else");

            // Set enter label
            builder.SetLabel(ContinueLabel);

            // Build condition and emit branch
            Condition.Build();
            builder.EmitJumpIfZero(Else != null ? elseLabel : BreakLabel);

            // Execute body and go back while entry
            Body?.Build();
            builder.EmitJump(ContinueLabel);

            // Else block
            if (Else != null)
            {
                builder.SetLabel(elseLabel);
                Else.Build();
            }

            // Exit
            builder.SetLabel(BreakLabel);
        }

        public override string ToString() => $"While {Condition}";
    }
}
