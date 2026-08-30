namespace SCode.Compiler.Ast.Statements
{
    public class ForStatement : Statement, BreakStatement.IBreakableFlow, ContinueStatement.IContinuableFlow
    {
        public string ContinueLabel { get; } = RandomGenerator.RandomStringLabel("for_continue");
        public string BreakLabel { get; } = RandomGenerator.RandomStringLabel("for_exit");

        [ChildNode]
        public Node? Initializer { get; set; }

        [ChildNode]
        public Expression? Condition { get; set; }

        [ChildNode]
        public Expression? Iterator { get; set; }

        [ChildNode]
        public Block? Body { get; set; }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
            var enterLabel = RandomGenerator.RandomStringLabel("for_enter");

            // Init statement
            Initializer?.Build();

            // Set enter label
            builder.SetLabel(enterLabel);

            // Build condition and emit branch
            if (Condition != null)
            {
                Condition.Build();
                builder.EmitJumpIfZero(BreakLabel);
            }

            // Execute body
            Body?.Build();

            // Iterator expression and repeat
            builder.SetLabel(ContinueLabel);
            Iterator?.Build();
            builder.EmitJump(enterLabel);

            // Exit
            builder.SetLabel(BreakLabel);
        }

        public override string ToString() => $"For ({Initializer} ; {Condition} ; {Iterator})";
    }
}
