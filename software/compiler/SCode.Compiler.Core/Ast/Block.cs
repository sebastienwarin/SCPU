namespace SCode.Compiler.Ast
{
    [NestedScope]
    public class Block : Node
    {
        [ChildNode]
        public List<Statement> Body { get; set; }
    }
}
