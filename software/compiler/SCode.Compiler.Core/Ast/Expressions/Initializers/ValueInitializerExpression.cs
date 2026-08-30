using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions.Initializers
{
    public class ValueInitializerExpression : InitializerExpression
    {
        [ChildNode]
        public Expression Value { get; set; }

        public override TypeInfo GetResultType()
        {
            return Value.GetResultType();
        }

        public override string ToString() => Value?.ToString() ?? nameof(ValueInitializerExpression);
    }
}
