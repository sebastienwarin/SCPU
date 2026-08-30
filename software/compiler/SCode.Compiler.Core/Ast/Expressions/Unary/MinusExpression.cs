namespace SCode.Compiler.Ast.Expressions.Unary
{
    public class MinusExpression : UnaryExpression
    {
        protected override void OnPrepare()
        {
            PrepareChildren();
            if (Target.GetResultType().TypeCode != Type.SCodeType.Int)
            {
                throw RaiseError($"The unary '-' operator is restricted to int types only.");
            }
        }

        protected override void OnBuild()
        {
            Target.Build();
            Context.InstructionBuilder.EmitNegateA();
        }
    }
}
