namespace SCode.Compiler.Ast.Expressions.Unary
{
    public class BitwiseNotExpression : UnaryExpression
    {
        protected override void OnPrepare()
        {
            PrepareChildren();
            var typeInfo = Target.GetResultType();
            if (!typeInfo.IsBaseType || typeInfo.TypeCode == Type.SCodeType.String)
            {
                throw RaiseError($"The '~' operator (bitwise NOT) is only applicable to basic types, excluding 'string'.");
            }
        }

        protected override void OnBuild()
        {
            Target.Build();
            Context.InstructionBuilder.EmitNotA();
        }
    }
}
