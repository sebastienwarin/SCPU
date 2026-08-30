using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public abstract class UnaryExpression : Expression
    {
        [ChildNode]
        public Expression Target { get; set; }

        public override TypeInfo GetResultType()
        {
            return Target.GetResultType();
        }

        public override string ToString() => $"({GetType().Name.Replace("Expression", "")} {Target})";
    }
}
