namespace SCode.Compiler.Ast.Expressions.Unary
{
    public class LogicalNotExpression : UnaryExpression
    {
        protected override void OnPrepare()
        {
            PrepareChildren();
            if (Target.GetResultType().TypeCode != Type.SCodeType.Bool)
            {
                throw RaiseError($"The '!' operator (logical NOT) is restricted to boolean types only.");
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;

            var labelFalse = RandomGenerator.RandomStringLabel("logicalnot_false");
            var labelExit = RandomGenerator.RandomStringLabel("logicalnot_exit");

            // Build target expression
            Target.Build();

            // If Value == False (#0), jump to "labelFalse"
            builder.EmitJumpIfZero(labelFalse);

            // Else If Value == True (#1), clear & exit
            builder.EmitClearA();
            builder.EmitJump(labelExit);

            // If False, return True (#1)
            builder.SetLabel(labelFalse);
            builder.EmitLoadA(1);

            builder.SetLabel(labelExit);
        }
    }
}
