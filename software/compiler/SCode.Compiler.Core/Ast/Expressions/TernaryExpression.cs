using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    internal class TernaryExpression : Expression
    {
        [ChildNode]
        public Expression Condition { get; set; }

        [ChildNode]
        public Expression True { get; set; }

        [ChildNode]
        public Expression False { get; set; }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;

            // Generate labels
            var labelFalse = RandomGenerator.RandomStringLabel("case_false");
            var labelExit = RandomGenerator.RandomStringLabel("case_exit");

            // Build condition
            Condition.Build();
            builder.EmitJumpIfZero(labelFalse);
            
            // If True
            True.Build();
            builder.EmitJump(labelExit);

            // If False
            builder.SetLabel(labelFalse);
            False.Build();

            // Exit label
            builder.SetLabel(labelExit);
        }

        public override TypeInfo GetResultType()
        {
            if (!TypeHelper.TryGetBinaryResultType(True.GetResultType(), False.GetResultType(), out var resultType))
            {
                throw RaiseError($"Incompatible types: Cannot determine a valid result type for the  binary expression of type '{this.GetType().Name}' between '{True}' and '{False}");
            }
            return resultType;
        }

        public override string ToString() => $"({Condition} ? {True} : {False})";
    }
}
