namespace SCode.Compiler.Ast.Statements
{
    public class ExpressionStatement : Statement
    {
        [ChildNode]
        public Expression Expression { get; set; }

        public override string ToString() => $"{Expression}";
    }
}
