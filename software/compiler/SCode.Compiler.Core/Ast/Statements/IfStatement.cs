namespace SCode.Compiler.Ast.Statements
{
    public class IfStatement : Statement
    {
        [ChildNode]
        public Expression Condition { get; set; }

        [ChildNode]
        public Block Then { get; set; }

        [ChildNode]
        public Block? Else { get; set; }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
            var elseLabel = RandomGenerator.RandomStringLabel("if_else");
            var exitLabel = RandomGenerator.RandomStringLabel("if_exit");

            // Build condition and emit branch
            Condition.Build();
            builder.EmitJumpIfZero(Else != null ? elseLabel : exitLabel);

            // Then block
            Then?.Build();

            // Else block
            if (Else != null)
            {
                builder.EmitJump(exitLabel);
                builder.SetLabel(elseLabel);
                Else.Build();
            }

            // Exit label
            builder.SetLabel(exitLabel);
        }

        public override string ToString() => $"If {Condition}";
    }
}
